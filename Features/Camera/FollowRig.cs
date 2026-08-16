using System.Numerics;
using AdventureGame.App;
using AdventureGame.Common;
using AdventureGame.Features.Character;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Numerics;
using Quark.Physics.Dimension3D.Shapes;
using Quark.Platform.Input;

namespace AdventureGame.Features.Camera;

struct FollowRig() {
    public Entity Target;
    public double Yaw;
    public double Pitch;
    public double Distance;
    public double FocusHeight;
    
    public double DeadzoneFrac = 0.02;
    public double VerticalBandFrac = 0.15;
    public double LeadTime = 0.8;
    public double PreferredPitch = 0.35;

    // How hard a nearby doorway holds the framing, and the yaw it holds it at. Written by
    // DoorApproachSystem; any weight above zero also stands the lazy recentre down.
    public double DoorWeight;
    public double DoorYaw;
    
    internal double actualYaw;
    internal double actualPitch;
    internal double actualDistance;
    internal double lookIdleTime;
}

sealed class FollowRigSystem(IInput input, RigidBodySimulation simulation, DoorTuning tuning) : ISystem {
    const double ZoomStep = 1.0;
    const double MinPitch = -0.35, MaxPitch = 1.25;
    const double MinDistance = 3.5, MaxDistance = 16.0;
    const double RotationDecay = 22.0;

    public static Entity SpawnRig(EntityCommands world, Entity target) =>
        world.Spawn(new CameraRig { FieldOfView = Math.PI / 3, NearPlane = 0.1, FarPlane = 500 })
            .At(new Vector3d(0, -14, 6))
            .With(new FollowRig { Target = target, Pitch = 0.35, Distance = 8, FocusHeight = 1.3 });

    readonly EventReader<CharacterEvents.Teleported> teleports = new();

    public void Update(World world, EntityCommands commands, float deltaTime) {
        RebaseAfterTeleport(world);

        var look = input.Delta(Controls.Look);
        var zoom = input.Delta(Controls.Zoom);

        if (input.Player is { Scheme: InputScheme.KeyboardMouse, Cursor.Effective: CursorMode.Visible }) {
            look = Vector2.Zero;
        }

        foreach (var row in world.Query<RelativeTransform, FollowRig, CameraRig>()) {
            ref var transform = ref row.Component1;
            ref var cam = ref row.Component2;
            ref var rig = ref row.Component3;
            
            // Reset look idle time
            cam.lookIdleTime = look.LengthSquared() > 0 ? 0 : cam.lookIdleTime + deltaTime; 

            // Mouse orbit + wheel zoom
            cam.Yaw += look.X;
            cam.Pitch = Math.Clamp(cam.Pitch + look.Y, MinPitch, MaxPitch);
            cam.Distance = Math.Clamp(cam.Distance - zoom * ZoomStep, MinDistance, MaxDistance);

            // A doorway takes the framing over as the character closes on it: keeping the camera
            // behind his back would drive it into the wall the moment he steps out of a door. The
            // pull scales with the weight - barely there at the edge of the approach, firm at the
            // threshold - and the mouse still moves under it.
            if (cam.DoorWeight > 0) {
                var toDoor = Utils.WrapAngle(cam.DoorYaw - cam.Yaw);
                cam.Yaw += toDoor * (1 - Utils.Exp2(-deltaTime * tuning.CameraPull * cam.DoorWeight));
            }

            // Smooth with exponential decay
            cam.actualYaw = Decay.ExpDecay(cam.actualYaw, cam.Yaw, RotationDecay, deltaTime);
            cam.actualPitch = Decay.ExpDecay(cam.actualPitch, cam.Pitch, RotationDecay, deltaTime);
            
            // Ensure target is alive and has a transform
            if (!world.IsAlive(cam.Target) || !world.TryGet<RelativeTransform>(cam.Target, out var targetTransform))
                continue;
            
            // Compute target
            var target = targetTransform.LocalTransform.Position + new Vector3d(0, 0, cam.FocusHeight);
            
            // Get character movement
            var targetVelocity = Vector3d.Zero;
            var grounded = true;
            
            if (world.TryGet<CharacterMovement>(cam.Target, out var movement)) {
                targetVelocity = movement.Velocity;
                grounded = movement.Grounded;
                
                // Lazy follow, unless a doorway is holding the framing: both write Yaw, and near a
                // door the doorway is the one that knows where the room is
                if (cam.DoorWeight <= 0
                    && cam.lookIdleTime > Constants.CameraRecenterGrace
                    && movement.ActualHorizontalSpeed > Constants.CameraRecenterMinSpeed) {
                    var moveYaw = Math.Atan2(movement.Velocity.X, movement.Velocity.Y);
                    var delta = Utils.WrapAngle(moveYaw - cam.Yaw);
                    
                    // Don't fight player walking away from or toward the camera
                    var alignment = Utils.Clamp01(Math.Cos(delta) + 0.99);
                    var speed01 = Utils.Clamp01(movement.ActualHorizontalSpeed / Constants.MaxSpeed);

                    var error = Math.Abs(delta);
                    var angleGate = Utils.SmoothStep01((error - Constants.CameraRecenterDeadzone) / Constants.CameraRecenterRamp);
                    
                    var strength = Constants.CameraRecenterDecay * alignment * speed01 * angleGate;

                    cam.Yaw += delta * (1 - Utils.Exp2(-deltaTime * strength));
                    cam.Pitch = Decay.ExpDecay(cam.Pitch, cam.PreferredPitch, strength * 0.5, deltaTime);
                }
            }
            
            // Update pivot (deadzone, lead time)
            UpdatePivot(ref cam, ref rig, target, targetVelocity, grounded, deltaTime);
            
            // Avoid occlusion (bring camera back)
            UpdateOcclusionDistance(ref cam, ref rig, cam.Distance, deltaTime);
            
            // Apply to rig
            rig.Yaw = Utils.WrapAngle(cam.actualYaw);
            rig.Pitch = Utils.WrapAngle(cam.actualPitch);
            rig.Distance = cam.actualDistance;
        }
    }

    // Carries the rig through a teleport instead of letting it chase: the pivot and the lead keep
    // their offset to the character, rotated by the same turn, and every smoothed value is untouched.
    // The camera therefore resumes its motion at the destination rather than flying across the world.
    //
    // Applied one frame late on purpose: the rig follows the character's transform, which is only
    // presented in LateUpdate - on the teleport frame it still shows the old spot, and rebasing
    // immediately would let UpdatePivot drag the pivot right back across the world.
    CharacterEvents.Teleported? pendingRebase;

    void RebaseAfterTeleport(World world) {
        if (pendingRebase is { } evt) {
            pendingRebase = null;
            foreach (var row in world.Query<FollowRig, CameraRig>()) {
                ref var cam = ref row.Component1;
                ref var rig = ref row.Component2;
                if (cam.Target != evt.Entity)
                    continue;

                rig.Pivot = evt.To + Utils.TurnZ(rig.Pivot - evt.From, evt.YawDelta);
                rig.Lead = Utils.TurnZ(rig.Lead, evt.YawDelta);
                cam.Yaw = Utils.WrapAngle(cam.Yaw + evt.YawDelta);
                cam.actualYaw = Utils.WrapAngle(cam.actualYaw + evt.YawDelta);
            }
        }

        foreach (var teleport in teleports.Read(world))
            pendingRebase = teleport;
    }

    void UpdatePivot(ref FollowRig cam, ref CameraRig rig, Vector3d target, Vector3d targetVelocity, bool grounded, float deltaTime) {
        // Deadzone is relative to framed height
        var framed = 2 * cam.actualDistance * Math.Tan(rig.FieldOfView * 0.5);
        var deadR = cam.DeadzoneFrac * framed;
        var bandZ = cam.VerticalBandFrac * framed;
        
        // Horizontal: Stick to cylinder around target
        var flat = new Vector3d(target.X, target.Y, rig.Pivot.Z);
        var offset = flat - rig.Pivot;
        var dist = offset.Length();
        if (dist > deadR) {
            var pivot = rig.Pivot + offset / dist * (dist - deadR);
            rig.Pivot = Decay.ExpDecay(rig.Pivot, pivot, 5, deltaTime);
        }
        
        // Vertical: Dissociated
        if (grounded) {
            // Follow target slowly
            rig.Pivot.Z = Decay.ExpDecay(rig.Pivot.Z, target.Z, 6, deltaTime);
        }
        else {
            // Restrict to bands
            var dz = target.Z - rig.Pivot.Z;
            if (Math.Abs(dz) > bandZ) {
                rig.Pivot.Z = Decay.ExpDecay(rig.Pivot.Z, target.Z, 3, deltaTime);
            }
        }

        // Recompute lead
        var lead = Utils.ClampLength(Utils.FlattenXY(targetVelocity) * cam.LeadTime, Constants.CameraMaxLead);
        rig.Lead = Decay.ExpDecay(rig.Lead, lead, 2, deltaTime);
    }
    void UpdateOcclusionDistance(ref FollowRig cam, ref CameraRig rig, double distance, float deltaTime) {
        // Estimate real camera position
        var forward = new Vector3d(Math.Sin(cam.actualYaw), Math.Cos(cam.actualYaw), 0);
        var horizontal = Math.Cos(cam.actualPitch) * distance;
        var height = Math.Sin(cam.actualPitch) * distance;
        var position = rig.Pivot - forward * horizontal + new Vector3d(0, 0, height);
        
        /* TODO:
         - Maybe add a timer before considering unoccluded
         - If object is considered as hideable, hide it instead of moving camera
         */ 
        
        // Sweep sphere from pivot to camera
        var sphere = new Sphere(0.15f); //Radius = near plane
        var origin = rig.Pivot;
        var motion = position - origin;
        var hit = simulation.Sweep(sphere, (Vector3)origin, (Vector3)motion, out var h, Layers.Environment);
        if (hit && h.Normal.LengthSquared() > 0) {
            var targetDistance = Math.Max(h.Distance - 0.3, Constants.CameraMinDistance);
            
            // If we need to move closer, do it quickly
            var decay = targetDistance < cam.actualDistance
                ? Constants.CameraOcclusionDecay
                : Constants.CameraDeocclusionDecay;
            
            cam.actualDistance = Decay.ExpDecay(cam.actualDistance, targetDistance, decay, deltaTime);
        }
        else {
            cam.actualDistance = Decay.ExpDecay(cam.actualDistance, distance, Constants.CameraRecenterDecay, deltaTime);
        }
    }
}
