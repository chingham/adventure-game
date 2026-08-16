using System.Numerics;
using AdventureGame.Common;
using Quark.Numerics;
using Quark.Kit.Components;
using Quark.Physics.Dimension3D.Shapes;

namespace AdventureGame.Features.Character;

// Getting out of whatever we ended up inside, before anything else this tick reads a position. What
// cannot be resolved is reported: being squeezed, and being stuck long enough to need a rescue.
partial class CharacterMovementSystem {
    void Depenetrate(ref CharacterMovement movement, ref MoveTick tick) {
        // Prepare overlap
        var (overlapShape, overlapOffset) = GetQueryShape();

        Span<RigidBodyContact> buffer = stackalloc RigidBodyContact[16];

        var layer = movement.CollisionMask;
        ref var position = ref movement.Position;
        ref var velocity = ref movement.Velocity;
        var budget = (double)MaxDepenetrationPerTick;

        for (var i = 0; i < MaximumDepenetrationPasses; i++) {
            // Get overlap bodies
            var count = simulation.Overlap(overlapShape, (Vector3)(position + overlapOffset), buffer, layer);

            // If no overlap, return, it's all fine
            if (count == 0) {
                movement.lastValidPosition = position;
                movement.stuckTicks = 0;
                return;
            }

            if (budget <= Utils.Epsilon) break;

            // Check for overflow
            if (count > buffer.Length)
                Console.WriteLine($"Warning: Depenetration buffer overflow, some contacts may be ignored. Suspect geometry at {position}");

            var n = Math.Min(count, buffer.Length);

            // Sort for determinism
            buffer[..n].Sort(SortContacts);

            // Being squeezed is read on the contacts as they stand, before we resolve any of them
            if (i == 0) tick.Crushed = IsCrushed(buffer[..n]);

            for (var k = 0; k < n; k++) {
                // Recompute penetration depth because previous iterations may have changed it
                if (!simulation.OverlapWith(overlapShape, (Vector3)(position + overlapOffset), buffer[k].Entity, out var contact)) {
                    // This contact is no longer valid, skip it
                    continue;
                }

                // Move to depenetrate, within the tick budget
                var push = Math.Min(contact.Depth + CharacterShape.SkinWidth * 0.5f, budget);
                if (push <= Utils.Epsilon) break;
                budget -= push;
                position += (Vector3d)contact.Normal * push;

                // Pushed out along a walkable normal: something solid rose into us, and being pushed
                // up IS being landed on. The downward probe cannot see it, we were already inside.
                if (!tick.DepenetrationGrounded && contact.Normal.Z >= tuning.WalkableCos) {
                    tick.DepenetrationGrounded = true;
                    tick.DepenetrationNormal = contact.Normal;
                    tick.DepenetrationEntity = contact.Entity;
                }

                // Kill velocity that goes into surface to avoid re-penetration on next frame
                var into = Vector3d.Dot(velocity, contact.Normal);
                if (into < 0)
                    velocity -= (Vector3d)contact.Normal * into;
            }
        }

        movement.stuckTicks++;
    }

    // Squeezed between two surfaces facing each other, with less free room left than the tolerance.
    // An extrusion sideways is not a crush: depenetration found somewhere to put us.
    static bool IsCrushed(ReadOnlySpan<RigidBodyContact> contacts) {
        for (var a = 0; a < contacts.Length; a++) {
            for (var b = a + 1; b < contacts.Length; b++) {
                if (Vector3d.Dot(contacts[a].Normal, contacts[b].Normal) > -0.5) continue;
                if (contacts[a].Depth + contacts[b].Depth > CrushTolerance) return true;
            }
        }
        return false;
    }
    static int SortContacts(RigidBodyContact a, RigidBodyContact b) {
        var depthCompare = a.Depth.CompareTo(b.Depth);
        if (depthCompare != 0) return depthCompare;
        return a.Entity.Index.CompareTo(b.Entity.Index);
    }

    void CheckCrush(ref CharacterMovement movement, ref MoveTick tick) {
        if (tick.Crushed || movement.stuckTicks > CrushStuckTicks)
            OnCrushed();
    }

    void RescueIfStuck(ref CharacterMovement movement) {
        if (movement.stuckTicks <= 60) return;

        // Check if last valid position is still valid
        var (overlapShape, overlapOffset) = GetQueryShape();
        var layer = movement.CollisionMask;

        var stillFree =
            !simulation.OverlapAny(overlapShape, (Vector3)(movement.lastValidPosition + overlapOffset), layer);
        if (stillFree) {
            // Teleport to last valid position
            movement.Position = movement.lastValidPosition;
            movement.Velocity = Vector3d.Zero;
            movement.TeleportedThisTick = true;
            Console.WriteLine($"Rescued character from stuck position after {movement.stuckTicks} ticks");
        }
        else {
            //TODO: Respawn
            Console.WriteLine($"TODO: Respawn character from stuck position after {movement.stuckTicks} ticks");
        }
    }
}
