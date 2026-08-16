using System.Numerics;
using AdventureGame.Common;
using AdventureGame.Features.Platforms;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Features.Character;

// What we are standing on, and what it does to us: finding the floor under the feet after the move,
// and riding whatever that floor is itself doing.
partial class CharacterMovementSystem {
    void ProbeGround(World world, Entity entity, ref CharacterMovement movement, ref MoveTick tick, float deltaTime) {
        var wasGrounded = movement.Grounded;
        movement.Grounded = false;
        movement.GroundNormal = Vector3d.UnitZ;
        movement.touchedSteep = false;
        movement.groundEntity = Entity.Null;

        ref var position = ref movement.Position;
        ref var velocity = ref movement.Velocity;

        // Do not check ground at the start of a jump
        if (movement.jumpLockTicks > 0) {
            movement.jumpLockTicks--;
        }
        // A step up already swept the surface under the feet, so it is authoritative for this tick
        else if (movement.stepUpsThisTick > 0) {
            movement.Grounded = true;
            movement.GroundNormal = Vector3d.UnitZ;
            if (velocity.Z < 0) velocity.Z = 0;
        }
        // Do not check ground if rising
        else if (velocity.Z <= 0) {
            // Sweep to catch ground below
            var layer = movement.CollisionMask;
            var probe = wasGrounded ? GroundProbeDistance(deltaTime) : CharacterShape.SkinWidth * 2;

            var (shape, offset) = GetQueryShape();
            var sweepOffset = position + offset;
            var sweepLen = probe + CharacterShape.SkinWidth;
            var sweepMotion = -Vector3d.UnitZ * sweepLen;

            Span<RigidBodyHit> sweepHits = stackalloc RigidBodyHit[8];
            var sweepCount = simulation.Sweep(shape, (Vector3)sweepOffset, (Vector3)sweepMotion, sweepHits, layer);

            // Nearest WALKABLE hit, not nearest hit: a refused ramp beside us, or the nose of the step
            // we are leaving, must not answer in place of the floor under our feet. The steepest of the
            // rest feeds the slide, and it can only come from here since this sweep looks down.
            var ground = default(RigidBodyHit);
            var steep = default(RigidBodyHit);
            var foundGround = false;
            var foundSteep = false;
            var overlapped = false;

            for (var i = 0; i < sweepCount; i++) {
                var candidate = sweepHits[i];

                if (!ValidHit(candidate)) {
                    overlapped = true;
                    continue;
                }

                var surface = candidate.Normal;
                if (Walkable(wasGrounded, candidate.Normal)) {
                    var origin = candidate.Position + Vector3d.UnitZ * CharacterShape.SkinWidth;
                    if (simulation.Raycast((Vector3)origin, -Vector3.UnitZ, CharacterShape.CapsuleRadius, out var truth, layer)
                        && truth.Normal.Z > SteepMinZ) {
                        surface = truth.Normal;
                    }
                }

                if (Walkable(wasGrounded, surface)) {
                    ground = candidate;
                    foundGround = true;
                    break;
                }

                if (!foundSteep
                    && surface.Z > SteepMinZ
                    && surface.Z < tuning.WalkableCos) {
                    steep = candidate;
                    foundSteep = true;
                }
            }

            if (foundGround) {
                movement.Grounded = true;
                movement.GroundNormal = ground.Normal;
                movement.groundEntity = ground.Entity;

                // Snap to ground when grounded
                position -= Vector3d.UnitZ * Math.Max(ground.Distance - CharacterShape.SkinWidth, 0);

                if (!wasGrounded) {
                    OnLanded(world, entity, ref movement, -velocity.Z);
                }

                // Reset vertical velocity
                if (velocity.Z < 0) velocity.Z = 0;
            }
            else if (foundSteep) {
                movement.steepNormal = steep.Normal;
                movement.touchedSteep = true;
            }
            else if (tick.DepenetrationGrounded) {
                // A rising platform can cross our feet between two ticks: our own sweep is sized on
                // our fall, not on how fast the world came to us. Depenetration already answered.
                movement.Grounded = true;
                movement.GroundNormal = tick.DepenetrationNormal;
                movement.groundEntity = tick.DepenetrationEntity;

                if (!wasGrounded) {
                    OnLanded(world, entity, ref movement, -velocity.Z);
                }

                if (velocity.Z < 0) velocity.Z = 0;
            }
            else if (overlapped || movement.stepUpGrace > 0) {
                // Overlapping, or mid-climb on a nose too steep to read as ground: hold the state and
                // let depenetration sort the position out.
                movement.Grounded = wasGrounded;
                if (movement.Grounded && velocity.Z < 0) velocity.Z = 0;
            }
        }

        // Smooth normal
        var smoothFactor = 1 - (-deltaTime / 0.05f).Exp2();
        movement.groundNormalSmoothed =
            Vector3d.Lerp(movement.groundNormalSmoothed, movement.GroundNormal, smoothFactor).Normalized();
    }

    // How far down to look: at least a step, and always further than we can have fallen off a slope
    // within one tick.
    double GroundProbeDistance(float deltaTime) {
        return Math.Max(
            tuning.StepDownHeight,
            1.5f * tuning.MaxSpeed * Math.Tan(tuning.WalkableAngle) * deltaTime + CharacterShape.SkinWidth);
    }

    // Hysteresis: what is steep enough to slide off is not what is steep enough to refuse to climb
    bool Walkable(bool grounded, Vector3d n) {
        return n.Z >= (grounded ? tuning.UnwalkableCos : tuning.WalkableCos);
    }

    // Moving platforms

    void ApplyPlatformDelta(World world, ref CharacterMovement movement, float deltaTime) {
        // Find riding platform
        var ridingPlatform = default(MovingPlatform);
        // The ground can be destroyed under the character (a level reload), so the handle is only
        // trusted while it is still alive.
        var isRidingPlatform = movement.Grounded
            && world.IsAlive(movement.groundEntity)
            && world.TryGet(movement.groundEntity, out ridingPlatform);

        // Keep inertia
        if (!isRidingPlatform) {
            movement.platformMemory = Math.Max(movement.platformMemory - deltaTime, 0);
            if (movement.platformMemory <= 0) movement.platformVelocity = Vector3d.Zero;
            return;
        }

        var delta = MovingPlatformDelta(ridingPlatform, movement.Position);
        var before = movement.Position;

        // Separate axes
        var layer = movement.CollisionMask;
        movement.Position = SweepStop(movement.Position, Utils.FlattenXY(delta), layer);
        movement.Position = SweepStop(movement.Position, new Vector3d(0, 0, delta.Z), layer);

        var achieved = movement.Position - before;

        // Rotate with platform
        movement.Yaw = Angle.Wrap(movement.Yaw + MovingPlatformYawDelta(ridingPlatform));

        movement.platformVelocity = achieved / deltaTime;
        movement.platformMemory = tuning.CoyoteTime;
    }

    static Vector3d MovingPlatformDelta(in MovingPlatform platform, Vector3d point) {
        var inv = platform.PreviousTransform.Inverse();
        var local = Vector3d.Transform(point, inv);
        return Vector3d.Transform(local, platform.CurrentTransform) - point;
    }
    static double MovingPlatformYawDelta(in MovingPlatform platform) {
        return ExtractYaw(platform.CurrentTransform) - ExtractYaw(platform.PreviousTransform);
    }
    static double ExtractYaw(in Matrix4x4d mtx) => Math.Atan2(mtx.M21, mtx.M22);
}
