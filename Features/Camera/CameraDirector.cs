using AdventureGame.Common;
using ImGuiNET;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Features.Camera;

struct CameraRig {
    public Vector3d Pivot;
    public Vector3d Lead;
    public double Yaw;
    public double Pitch;
    public double Distance;
    public double FieldOfView;
    public double NearPlane;
    public double FarPlane;

    public static CameraRig Blend(CameraRig a, CameraRig b, double t) {
        return new CameraRig {
            Pivot = Vector3d.Lerp(a.Pivot, b.Pivot, t),
            Lead = Vector3d.Lerp(a.Lead, b.Lead, t),
            Yaw = Utils.WrapAngle(Utils.LerpAngle(a.Yaw, b.Yaw, t)),
            Pitch = double.Lerp(a.Pitch, b.Pitch, t),
            Distance = double.Lerp(a.Distance, b.Distance, t),
            FieldOfView = Utils.LerpFov(a.FieldOfView, b.FieldOfView, t),
            NearPlane = double.Lerp(a.NearPlane, b.NearPlane, t),
            FarPlane = double.Lerp(a.FarPlane, b.FarPlane, t),
        };
    }
}

// Two axes rather than three weights. Target picks the base shot (follow outside, isometric inside)
// and is smoothed; Approach then pulls that base toward the doorway shot and is not, so the player's
// own pace scrubs it. At Approach 1 the doorway rig owns the whole image, which is what lets a portal
// swap the base underneath without anyone seeing it.
struct CameraDirector {
    public Entity FollowRig;
    public Entity IsometricRig;
    public Entity DoorRig;

    public double Target;
    public double Approach;
    public double Yaw;

    // Base shot follows the space the character stands in; the toggle inverts that choice rather
    // than replacing it, so it stays an override wherever the player goes.
    public double InSpace;
    public bool Flipped;

    internal double ActualTarget;

    // Snaps the base shot, for the frame a portal crossing hides it.
    public void SnapTarget(double target) {
        Target = target;
        ActualTarget = target;
    }
}

class CameraDirectorSystem(IInput input) : ISystem {
    const double DecayValue = 8;
    
    public void Update(World world, EntityCommands commands, float deltaTime) {
        // Input
        //var toggle = input.Button(Controls.ToggleView);
        
        foreach (var e in world.Query<CameraDirector, RelativeTransform, CameraComponent>()) {
            ref var director = ref e.Component1;
            ref var transform = ref e.Component2;
            ref var camera = ref e.Component3;
            
            // The space picks the base shot, the toggle inverts it
            //if (toggle == ButtonState.JustPressed) {
            //    director.Flipped = !director.Flipped;
            //}
            director.Target = director.Flipped ? 1 - director.InSpace : director.InSpace;

            // Update with decay
            director.ActualTarget = Decay.ExpDecay(director.ActualTarget, director.Target, DecayValue, deltaTime);
        
            // Get the three rigs
            var followRig = world.Get<CameraRig>(director.FollowRig);
            var isometricRig = world.Get<CameraRig>(director.IsometricRig);
            var doorRig = world.Get<CameraRig>(director.DoorRig);

            // Interpolate: base shot first, then the doorway shot over it
            var baseRig = CameraRig.Blend(followRig, isometricRig, director.ActualTarget);
            var rig = CameraRig.Blend(baseRig, doorRig, director.Approach);
            
            // Apply to camera
            transform.LocalTransform = ComputeTransform(rig.Pivot, rig.Lead, rig.Yaw, rig.Pitch, rig.Distance);
            camera.FieldOfView = rig.FieldOfView;
            camera.NearPlane = rig.NearPlane;
            camera.FarPlane = Math.Max(rig.FarPlane, camera.NearPlane + 1);
            
            // Update director yaw
            director.Yaw = rig.Yaw;
            
            //--- IMGUI ---//
            if (!ImGui.Begin("Camera Director")) {
                ImGui.End();
                return;
            }

            ImGui.Text($"Actual Target: {director.ActualTarget:F2}");
            ImGui.Text($"Approach: {director.Approach:F2}");
        
            ImGui.End();

            break;
        }
    }

    static Transform ComputeTransform(Vector3d pivot, Vector3d lead, double yaw, double pitch, double distance) {
        var forward = new Vector3d(Math.Sin(yaw), Math.Cos(yaw), 0);
        var horizontal = Math.Cos(pitch) * distance;
        var height = Math.Sin(pitch) * distance;

        var position = pivot - forward * horizontal + new Vector3d(0, 0, height);
        var facing = Rotations.LookAt(position, pivot + lead);
            
        return new Transform(position, facing);
    }
}