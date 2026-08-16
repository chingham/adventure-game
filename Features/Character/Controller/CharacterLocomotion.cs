using AdventureGame.Common;
using Quark.Ecs;
using Quark.Numerics;

namespace AdventureGame.Features.Character;

// Turning intent into a velocity, before anything is swept against the world: how fast we get up to
// speed, what gravity applies right now, when a jump is allowed, and where the body faces.
partial class CharacterMovementSystem {
    void UpdateHorizontalVelocity(ref CharacterMovement movement, CharacterIntent intent, float deltaTime) {
        var horizontal = Utils.FlattenXY(movement.Velocity);
        var target = intent.Direction * intent.Speed;

        // On walkable ground, do not lose speed
        if (movement.Grounded) {
            target = Utils.ProjectAndScale(target, movement.groundNormalSmoothed);
        }

        // Control on steep slide
        if (!movement.Grounded && movement.touchedSteep) {
            //NOTE: Commented because this causes jump to go lower when against a wall
            var downhill = Vector3d.ProjectOnPlane(-Vector3d.UnitZ, movement.steepNormal);
            if (downhill.LengthSquared() > Utils.Epsilon) {
                downhill = downhill.Normalized();
                movement.Velocity.Z += downhill.Z * tuning.SlideAcceleration * deltaTime;
                horizontal += Utils.FlattenXY(downhill) * tuning.SlideAcceleration * deltaTime;

                target = target * tuning.SlideControl
                    + Utils.FlattenXY(downhill) * (1 - tuning.SlideControl) * tuning.MaxSpeed;
            }
        }

        // Interpolate between accelerating and braking
        var hl = horizontal.Length();
        var dot = intent.Speed < 0.01 ? -1
            : hl < Utils.Epsilon ? 1
            : Vector3d.Dot(intent.Direction, horizontal / hl);
        var rate = movement.Grounded
            ? double.Lerp(tuning.MaxSpeed / tuning.TimeToMaxSpeed, tuning.MaxSpeed / tuning.TimeToStop,
                (-dot).Clamp01())
            : movement.ActualHorizontalSpeed < 0.5
                ? tuning.MaxSpeed / tuning.TimeToMaxSpeed
                : tuning.MaxSpeed / tuning.AirTimeToMax;

        horizontal = Vector3d.MoveTowards(horizontal, target, rate * deltaTime);
        movement.Velocity.X = horizontal.X;
        movement.Velocity.Y = horizontal.Y;
    }

    void UpdateVerticalVelocity(ref CharacterMovement movement, float deltaTime) {
        ref var velocity = ref movement.Velocity;

        // Do not add gravity when grounded
        if (movement.Grounded && velocity.Z <= 0) {
            velocity.Z = 0;
            return;
        }

        var rising = velocity.Z > 0;
        var cutting = rising && movement.jumpCutArmed && !movement.jumpHeld;

        var g = rising
            ? tuning.JumpRiseGravity * (cutting ? tuning.JumpCutGravityScale : 1)
            : tuning.JumpRiseGravity * tuning.FallMultipler;

        // No apex when cut, we want it to land ASAP
        if (rising && !cutting && velocity.Z < tuning.JumpApexThreshold) g *= tuning.JumpApexGravityScale;

        velocity.Z = Math.Max(-tuning.TerminalVelocity, velocity.Z - g * deltaTime);
    }

    // Jump

    void UpdateJump(World world, Entity entity, ref CharacterMovement movement, CharacterIntent intent, float deltaTime) {
        movement.jumpHeld = intent.JumpHeld;

        // Update timers
        movement.coyoteTimer = movement.Grounded ? tuning.CoyoteTime : Math.Max(0, movement.coyoteTimer - deltaTime);
        movement.bufferTimer = intent.JumpPressed ? tuning.JumpBufferTime : Math.Max(0, movement.bufferTimer - deltaTime);

        // Trigger
        if (movement.bufferTimer > 0 && movement.coyoteTimer > 0) {
            movement.Velocity.Z = tuning.JumpSpeed;
            movement.Grounded = false;
            movement.coyoteTimer = 0;
            movement.bufferTimer = 0;
            movement.jumpLockTicks = 3;
            movement.jumpCutArmed = true;

            OnJumpStarted(world, entity, ref movement);
        }
    }

    // Facing

    void UpdateFacing(ref CharacterMovement movement, CharacterIntent intent, float deltaTime) {
        // Only update facing when we are actually moving
        if (intent.Speed < 0.01f) return;

        var targetYaw = Math.Atan2(intent.Direction.X, intent.Direction.Y);

        // Snap when stopped
        if (movement.ActualHorizontalSpeed < tuning.TurnSnapBelowSpeed) {
            movement.Yaw = targetYaw;
            return;
        }

        // Degressive rotation
        var speed01 = (movement.ActualHorizontalSpeed / tuning.MaxSpeed).Clamp01();
        var maxStep = tuning.TurnSpeed * double.Lerp(1, 0.45, speed01) * deltaTime;

        var delta = Angle.Wrap(targetYaw - movement.Yaw);
        movement.Yaw = Angle.Wrap(movement.Yaw + double.Clamp(delta, -maxStep, maxStep));
    }

    // What the mesh turns to, always a little behind the yaw the controller works in
    void UpdateVisualYaw(ref CharacterMovement movement, float deltaTime) {
        var previous = movement.VisualYaw;
        var d = Angle.Wrap(movement.Yaw - movement.VisualYaw);
        movement.VisualYaw = Angle.Wrap(movement.VisualYaw + d * (1 - (-deltaTime / 0.05f).Exp2()));
        movement.TurnRate = Angle.Wrap(movement.VisualYaw - previous) / deltaTime;
    }
}
