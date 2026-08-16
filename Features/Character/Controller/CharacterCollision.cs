using System.Numerics;
using AdventureGame.Common;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Features.Character;

// Moving the body through the world: sweep, slide along what it meets, climb what is low enough to be
// a step. Horizontal and vertical are two separate passes, ordered by which way we are going.
partial class CharacterMovementSystem {
    void CollideAndSlide(ref CharacterMovement movement, ref MoveTick tick, float deltaTime) {
        movement.slideIterations = 0;
        movement.touchedSteep = false;
        tick.Contacts.Clear();

        var motion = (movement.Velocity + movement.carryVelocity) * deltaTime;
        var h = Utils.FlattenXY(motion);
        var v = new Vector3d(0, 0, motion.Z);

        if (motion.Z > 0) {
            // We're going up up up
            Slide(ref movement, ref tick, v, 0, Vector3d.Zero, verticalPass: true);
            Slide(ref movement, ref tick, h, 0, Vector3d.Zero, verticalPass: false);
        }
        else {
            // Never gonna let you dooown
            Slide(ref movement, ref tick, h, 0, Vector3d.Zero, verticalPass: false);
            Slide(ref movement, ref tick, v, 0, Vector3d.Zero, verticalPass: true);
        }
    }

    void Slide(ref CharacterMovement movement, ref MoveTick tick, Vector3d motion, int depth,
        Vector3d previousNormal, bool verticalPass) {
        ref var position = ref movement.Position;
        var layer = movement.CollisionMask;

        // Avoid infinite loops
        if (depth >= MaximumSlidePasses) {
            movement.exhaustedPasses++;
            return;
        }

        movement.slideIterations = Math.Max(movement.slideIterations, depth + 1);

        // Sweep capsule along motion vector
        var dist = motion.Length();
        if (dist < Utils.Epsilon) return;
        var dir = motion / dist;

        var (shape, offset) = GetQueryShape();
        var sweepOffset = position + offset;
        var sweepMotion = dir * (dist + CharacterShape.SkinWidth);
        var sweepHit = simulation.Sweep(shape, (Vector3)sweepOffset, (Vector3)sweepMotion, out var hit, layer);
        if (!sweepHit) {
            // No hit, advance freely
            position += motion;
            return;
        }
        if (!ValidHit(hit)) {
            // Let depenetration handle this
            return;
        }

        // Add to contact list
        tick.Contacts.Add(new MoveContact(hit.Normal, hit.Position, hit.Entity, verticalPass));

        // Advance to contact (minus skin)
        var advance = Math.Max(hit.Distance - CharacterShape.SkinWidth, 0);
        var leftover = motion - dir * advance;
        position += dir * advance;

        // Is the hit surface walkable ?
        var walkable = hit.Normal.Z >= tuning.WalkableCos;
        if (walkable) {
            // On vertical pass, this is a landing, we can stop here
            if (verticalPass) return;

            leftover = movement.Grounded
                ? Utils.ProjectAndScale(leftover, hit.Normal)
                : Vector3d.ProjectOnPlane(leftover, hit.Normal);
        }
        else {
            // Step up first: a capsule meets a low step on its top edge, so the contact normal comes
            // back tilted and cannot tell a step from a slope. Only the probes in TryStepUp can.
            if (movement.Grounded && !verticalPass && TryStepUp(ref movement, leftover, hit.Normal))
                return;

            // Scale to simulate friction against the wall
            var scale = 1d;
            if (Utils.TryFlatDir(hit.Normal, out var n) && Utils.TryFlatDir(-motion, out var m)) {
                scale = (1 - Vector3d.Dot(n, m)).Clamp01();
            }

            leftover = (movement.Grounded && !verticalPass)
                ? Utils.ProjectOnPlaneXY(leftover, hit.Normal) * scale
                : Vector3d.ProjectOnPlane(leftover, hit.Normal) * scale;
        }

        // If this is a second contact
        if (depth > 0 && Vector3d.Dot(leftover, previousNormal) < 0) {
            // Only slide along the crease to avoid corner dance
            var crease = Vector3d.Cross(previousNormal, hit.Normal);
            var len = crease.Length();
            if (len < Utils.Epsilon) leftover = Vector3d.Zero;
            else leftover = crease / len * Vector3d.Dot(leftover, crease / len);
        }

        // Guard if stopped (only if not walkable)
        if (!walkable && advance < Utils.Epsilon && leftover.LengthSquared() >= dist * dist * 0.999f) {
            return;
        }

        // Recurse (TODO: Optimize with a loop)
        Slide(ref movement, ref tick, leftover, depth + 1, hit.Normal, verticalPass);
    }

    // Steps

    bool TryStepUp(ref CharacterMovement movement, Vector3d leftover, Vector3d contactNormal) {
        ref var position = ref movement.Position;
        var layer = movement.CollisionMask;

        // Only step up if we have horizontal motion
        if (!Utils.TryFlatDir(leftover, out var fwd)) return false;
        var move = Utils.FlattenXY(leftover).Length();

        // Probe straight into the obstacle, never along the motion: an angled approach would otherwise
        // shorten the probes by cos(angle) and make the whole test depend on how we walked in.
        if (!Utils.TryFlatDir(-contactNormal, out var into)) into = fwd;

        // Prepare for sweeps
        var (shape, offset) = GetQueryShape();

        // How much can we lift
        var sweepMotion = Vector3d.UnitZ * (tuning.StepUpHeight + CharacterShape.SkinWidth);
        var blocked = simulation.Sweep(shape, (Vector3)(position + offset), (Vector3)sweepMotion, out var up, layer);

        var lift = blocked ? Math.Max(up.Distance - CharacterShape.SkinWidth, 0) : tuning.StepUpHeight;
        var raised = position + Vector3d.UnitZ * lift;
        var drop = lift + CharacterShape.SkinWidth * 2;

        // Is there a real ledge behind, or a slope or wall we should be refusing? A ray reads the
        // surface we would end up standing on; the capsule would only ever catch the edge in front.
        var probe = raised + into * (CharacterShape.CapsuleRadius + StepLedgeMargin);
        if (!simulation.Raycast((Vector3)probe, -Vector3.UnitZ, (float)drop, out var ledge, layer)) return false;
        if (!Walkable(false, ledge.Normal)) return false;

        // Reject when the ray came back down to our own floor: a wall too tall to climb would read as
        // a valid landing otherwise.
        if (lift - ledge.Distance < StepLedgeMinRise) return false;

        // Can we carry this tick's motion up there
        sweepMotion = fwd * (move + CharacterShape.SkinWidth);
        if (simulation.Sweep(shape, (Vector3)(raised + offset), (Vector3)sweepMotion, out _, layer)) return false;

        var advanced = raised + fwd * move;

        // Where do we land, step nose included
        sweepMotion = -Vector3d.UnitZ * drop;
        if (!simulation.Sweep(shape, (Vector3)(advanced + offset), (Vector3)sweepMotion, out var down, layer)) return false;
        if (!ValidHit(down) || down.Normal.Z <= SteepMinZ) return false;

        // Did we really go up? Compare against the bare contact: the skin gap added back would read as
        // a rise of one skin even when we simply fell onto the floor we started from.
        var contactZ = advanced.Z - down.Distance;
        if (contactZ - position.Z < Utils.Epsilon) return false;

        // Update position
        position = new Vector3d(advanced.X, advanced.Y, contactZ + CharacterShape.SkinWidth);
        movement.stepUpsThisTick++;
        movement.stepUpGrace = StepUpGraceTicks;
        return true;
    }

    // Reacting to what was hit on the way

    void ResolveContacts(ref CharacterMovement movement, ref MoveTick tick, Vector3d before, float deltaTime) {
        foreach (var c in tick.Contacts)
            ReactToCeiling(ref movement, c, deltaTime);

        movement.ActualHorizontalSpeed = Utils.FlattenXY((movement.Position - before) / deltaTime).Length();
    }

    void ReactToCeiling(ref CharacterMovement movement, in MoveContact contact, float deltaTime) {
        // Ceiling threshold guard
        if (contact.Normal.Z > -0.1 || movement.Velocity.Z <= 0) return;

        // Deviate
        movement.Velocity = Vector3d.ProjectOnPlane(movement.Velocity, contact.Normal);

        TryCornerCorrect(ref movement, deltaTime);
    }

    bool TryCornerCorrect(ref CharacterMovement movement, float deltaTime) {
        /*if (movement.cornerCorrectUsed || movement.Velocity.Z <= 0) return false;

        var fwd = Utils.TryFlatDir(movement.Velocity, out var f) ? f : Vector3d.Zero;
        var side = Vector3d.Cross(fwd, Vector3d.UnitZ);

        var (shape, offset) = GetQueryShape();

        Span<Vector3d> tries = [fwd, side, -side, -fwd];
        for (var i = 0; i < 4; i++) {
            if (tries[i].LengthSquared() < Utils.Epsilon) continue;

            var candidate = Slide(
                movement.Position,
                tries[i] * NudgeDistance,
                0,
                Vector3d.Zero,
                movement.CollisionMask,
                movement.Grounded,
                verticalPass: false);
            if ((candidate - movement.Position).Length() < NudgeDistance * 0.9) continue;

            var overlapOffset = offset + candidate + Vector3d.UnitZ * (movement.Velocity.Z * deltaTime);
            if (simulation.OverlapAny(shape, (Vector3)overlapOffset, movement.CollisionMask))
                continue;

            movement.Position = candidate;
            movement.cornerCorrectUsed = true;
            return true;
        }*/

        return false;
    }
}
