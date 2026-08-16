using System.Numerics;
using AdventureGame.App;
using AdventureGame.Common;
using AdventureGame.Features.Platforms;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;
using Quark.Physics.Dimension3D.Shapes;

namespace AdventureGame.Features.Character;

/*
 * Character movement
 * The core of the character controller
 * Updates the character transform based on intent and scene around them
 */

struct CharacterMovement() {
    public PhysicsLayer CollisionMask = Layers.Environment;
    
    public Vector3d Position;
    public Vector3d Velocity;
    
    public double Yaw;
    public double VisualYaw;
    
    public Vector3d PreviousPosition;
    public double PreviousVisualYaw;
    public double TurnRate;
    
    
    public bool TeleportedThisTick;
    public bool Grounded;
    public Vector3d GroundNormal = Vector3d.UnitZ;
    public double ActualHorizontalSpeed;

    // Internal state

    internal int slideIterations, exhaustedPasses;
    
    internal double coyoteTimer, bufferTimer;
    internal int jumpLockTicks;
    internal bool wasGrounded, jumpCutArmed, jumpHeld;
    internal int stuckTicks;
    
    internal Entity groundEntity;
    internal Vector3d platformVelocity;
    internal double platformMemory;
    internal Vector3d carryVelocity;

    internal Vector3d steepNormal;
    internal bool touchedSteep;

    internal int stepUpsThisTick;
    internal int stepUpGrace;

    internal bool cornerCorrectUsed;
}

sealed unsafe class CharacterMovementSystem(RigidBodySimulation simulation, CharacterTuning tuning) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {

        // Update characters
        foreach (var row in world.Query<CharacterIntent, CharacterMovement>()) {
            var intent = row.Component1;
            ref var movement = ref row.Component2;
            //ref var body = ref row.Component3;
            
            UpdateCharacter(world, row.Entity, intent, ref movement, deltaTime);
        }
    }

    // Solver limits and epsilons: they guard the algorithm rather than describe the character, so
    // they are not on the tuning sheet.
    const int MaximumDepenetrationPasses = 3;
    const int MaximumSlidePasses = 5;
    const float MaxDepenetrationPerTick = 0.5f;
    const float NudgeDistance = 0.14f;
    const float SteepMinZ = 0.1f;
    const float CrushTolerance = 0.05f;
    const int CrushStuckTicks = 10;
    const int StepUpGraceTicks = 3;
    const float StepLedgeMargin = 0.05f;
    const float StepLedgeMinRise = 0.05f;

    int slideIterations;
    int exhaustedPasses;
    bool depenetrationKilledVelocity;
    bool crushedThisTick;
    bool depenetrationGrounded;
    Vector3d depenetrationNormal;
    Entity depenetrationEntity;
    int stuckTicks;
    int stepUpsThisTick;
    int stepUpGrace;
    Vector3d lastValidPosition;
    //double lastValidTime;
    Vector3d groundNormalSmoothed = Vector3d.UnitZ;
    Entity groundEntity;
    double groundClamp;
    PhysicsLayer groundLayer;

    readonly List<Contact> contacts = new(8);
    
    void UpdateCharacter(
        World world,
        Entity entity,
        CharacterIntent intent, 
        ref CharacterMovement movement,
        float deltaTime) {
        
        // Prepare and reset
        exhaustedPasses = movement.exhaustedPasses;
        stepUpsThisTick = movement.stepUpsThisTick;
        stepUpGrace = movement.stepUpGrace;
        stuckTicks = movement.stuckTicks;
        
        movement.TeleportedThisTick = false;
        movement.PreviousPosition = movement.Position;
        movement.PreviousVisualYaw  = movement.VisualYaw;
        
        stepUpsThisTick = 0;
        if (stepUpGrace > 0) stepUpGrace--;
        
        // Depenetrate
        Depenetrate(ref movement);
        RescueIfStuck(ref movement);
        CheckCrush();
        
        // Moving platform
        ApplyPlatformDelta(world, ref movement, deltaTime);

        // Memoize start position
        var startPosition = movement.Position;
        
        // Update velocity
        UpdateHorizontalVelocity(ref movement, intent, deltaTime);
        UpdateVerticalVelocity(ref movement, intent, deltaTime);
        UpdateJump(world, entity, ref movement, intent, deltaTime);
        
        // Facing
        UpdateFacing(ref movement, intent, deltaTime);
        UpdateVisualYaw(ref movement, deltaTime);
        
        // Collide and slide
        CollideAndSlide(ref movement, deltaTime);
        movement.carryVelocity *= Utils.Exp2(-deltaTime / tuning.CarryHalfLife);
        
        // Ground
        ProbeGround(world, entity, ref movement, deltaTime);
        
        // Contacts
        ResolveContacts(ref movement, startPosition, deltaTime);
        
        // Update movement
        movement.slideIterations = slideIterations;
        movement.exhaustedPasses = exhaustedPasses;
        movement.stepUpsThisTick = stepUpsThisTick;
        movement.stepUpGrace = stepUpGrace;
        movement.groundEntity = groundEntity;
        movement.stuckTicks = stuckTicks;
        
        // If teleported, overwrite Previous*
        if (movement.TeleportedThisTick) {
            movement.PreviousPosition = movement.Position;
            movement.PreviousVisualYaw = movement.VisualYaw;
        }
        
        // Update anim params
        if (world.Has<CharacterAnimParams>(entity)) {
            ref var anim = ref world.Get<CharacterAnimParams>(entity); 
            UpdateAnimParams(ref movement, ref anim, deltaTime);
        }
    }

    static (QueryShape shape, Vector3d offset) GetQueryShape() {
        var shape = new Capsule(CharacterShape.CapsuleRadius, CharacterShape.CapsuleSegmentHeight);
        var offset = new Vector3d(0, 0, CharacterShape.CapsuleRestHeight);
        return (shape, offset);
    }

    void Depenetrate(ref CharacterMovement movement) {
        depenetrationKilledVelocity = false;
        crushedThisTick = false;
        depenetrationGrounded = false;
        depenetrationNormal = Vector3d.UnitZ;
        depenetrationEntity = Entity.Null;

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
                lastValidPosition = position;
                //lastValidTime = time;
                stuckTicks = 0;
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
            if (i == 0) crushedThisTick = IsCrushed(buffer[..n]);

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
                if (!depenetrationGrounded && contact.Normal.Z >= tuning.WalkableCos) {
                    depenetrationGrounded = true;
                    depenetrationNormal = contact.Normal;
                    depenetrationEntity = contact.Entity;
                }

                // Kill velocity that goes into surface to avoid re-penetration on next frame
                var into = Vector3d.Dot(velocity, contact.Normal);
                if (into < 0) {
                    velocity -= (Vector3d)contact.Normal * into;
                    depenetrationKilledVelocity = true;
                }
            }
        }

        stuckTicks++;
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
        var depthCompare  = a.Depth.CompareTo(b.Depth);
        if (depthCompare != 0) return depthCompare;
        return a.Entity.Index.CompareTo(b.Entity.Index);
    }

    void RescueIfStuck(ref CharacterMovement movement) {
        if (stuckTicks <= 60) return;
        
        // Check if last valid position is still valid
        var (overlapShape, overlapOffset) = GetQueryShape();
        var layer  = movement.CollisionMask;
        
        var stillFree = !simulation.OverlapAny(overlapShape, (Vector3)(lastValidPosition + overlapOffset), layer);
        if (stillFree) {
            // Teleport to last valid position
            movement.Position = lastValidPosition;
            movement.Velocity = Vector3d.Zero;
            movement.TeleportedThisTick = true;
            Console.WriteLine($"Rescued character from stuck position after {stuckTicks} ticks");
        }
        else {
            //TODO: Respawn
            Console.WriteLine($"TODO: Respawn character from stuck position after {stuckTicks} ticks");
        }
    }

    void CollideAndSlide(ref CharacterMovement movement, float deltaTime) {
        slideIterations = 0;
        movement.touchedSteep = false;
        contacts.Clear();

        var motion = (movement.Velocity + movement.carryVelocity) * deltaTime;
        var h = Utils.FlattenXY(motion);
        var v = new Vector3d(0, 0, motion.Z);

        if (motion.Z > 0) {
            // We're going up up up
            Slide(ref movement, v, 0, Vector3d.Zero, verticalPass: true);
            Slide(ref movement, h, 0, Vector3d.Zero, verticalPass: false);
        }
        else {
            // Never gonna let you dooown
            Slide(ref movement, h, 0, Vector3d.Zero, verticalPass: false);
            Slide(ref movement, v, 0, Vector3d.Zero, verticalPass: true);
        }
    }
    void Slide(ref CharacterMovement movement, Vector3d motion, int depth, Vector3d previousNormal, bool verticalPass) {
        ref var position = ref movement.Position;
        var layer = movement.CollisionMask;
        
        // Avoid infinite loops
        if (depth >= MaximumSlidePasses) {
            exhaustedPasses++;
            return;
        }

        slideIterations = Math.Max(slideIterations, depth + 1);

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
        contacts.Add(new Contact(hit.Normal, hit.Position, hit.Entity, verticalPass));

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
                : Utils.ProjectOnPlane(leftover, hit.Normal);
        }
        else {
            // Step up first: a capsule meets a low step on its top edge, so the contact normal comes
            // back tilted and cannot tell a step from a slope. Only the probes in TryStepUp can.
            if (movement.Grounded && !verticalPass && TryStepUp(ref movement, leftover, hit.Normal))
                return;

            // Scale to simulate friction against the wall
            var scale = 1d;
            if (Utils.TryFlatDir(hit.Normal, out var n) && Utils.TryFlatDir(-motion, out var m)) {
                scale = Utils.Clamp01(1 - Vector3d.Dot(n, m));
            }

            leftover = (movement.Grounded && !verticalPass)
                ? Utils.ProjectOnPlaneXY(leftover, hit.Normal) * scale
                : Utils.ProjectOnPlane(leftover, hit.Normal) * scale;
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
        Slide(ref movement, leftover, depth + 1, hit.Normal, verticalPass);
    }
    
    void ResolveContacts(ref CharacterMovement movement, Vector3d before, float deltaTime) {
        foreach (var c in contacts) {
            ReactToCeiling(ref movement, c, deltaTime);
        }
        
        movement.ActualHorizontalSpeed = Utils.FlattenXY((movement.Position - before) / deltaTime).Length();
    }
    void ReactToCeiling(ref CharacterMovement movement, in Contact contact, float deltaTime) {
        // Ceiling threshold guard
        if (contact.Normal.Z > -0.1 || movement.Velocity.Z <= 0) return;
        
        // Deviate
        movement.Velocity = Utils.ProjectOnPlane(movement.Velocity, contact.Normal);

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
        stepUpsThisTick++;
        stepUpGrace = StepUpGraceTicks;
        return true;
    }
    
    void UpdateHorizontalVelocity(ref CharacterMovement movement, CharacterIntent intent, float deltaTime) {
        var horizontal = Utils.FlattenXY(movement.Velocity);
        var target = intent.Direction * intent.Speed;
        
        // On walkable ground, do not lose speed
        if (movement.Grounded) {
            target = Utils.ProjectAndScale(target, groundNormalSmoothed);
        }
        
        // Control on steep slide
        if (!movement.Grounded && movement.touchedSteep) {
            //NOTE: Commented because this causes jump to go lower when against a wall
            var downhill = Utils.ProjectOnPlane(-Vector3d.UnitZ, movement.steepNormal);
            if (downhill.LengthSquared() > Utils.Epsilon) {
                downhill = downhill.Normalized();
                movement.Velocity.Z += downhill.Z * tuning.SlideAcceleration * deltaTime;
                horizontal += Utils.FlattenXY(downhill) * tuning.SlideAcceleration * deltaTime;
                
                target = target * tuning.SlideControl + Utils.FlattenXY(downhill) * (1 - tuning.SlideControl) * tuning.MaxSpeed;
            }
        }
        
        // Interpolate between accelerating and braking
        var hl = horizontal.Length();
        var dot = intent.Speed < 0.01 ? -1
            : hl < Utils.Epsilon ? 1
            : Vector3d.Dot(intent.Direction, horizontal / hl);
        var rate = movement.Grounded
            ? double.Lerp(tuning.MaxSpeed / tuning.TimeToMaxSpeed, tuning.MaxSpeed / tuning.TimeToStop,
                Utils.Clamp01(-dot))
            : movement.ActualHorizontalSpeed < 0.5
                ? tuning.MaxSpeed / tuning.TimeToMaxSpeed
                : tuning.MaxSpeed / tuning.AirTimeToMax;
        
        horizontal = Utils.MoveTowards(horizontal, target, rate * deltaTime);
        movement.Velocity.X = horizontal.X;
        movement.Velocity.Y = horizontal.Y;
    }
    void UpdateVerticalVelocity(ref CharacterMovement movement, CharacterIntent intent, float deltaTime) {
        ref var velocity = ref movement.Velocity;

        // Do not add gravity when grounded
        if (movement.Grounded && velocity.Z <= 0) {
            velocity.Z = 0;
            
            var groundProbe = GroundProbeDistance(deltaTime);
            groundClamp = Math.Min(tuning.StickToGround, groundProbe * 0.5f);
            return;
        }

        groundClamp = 0;

        var rising = velocity.Z > 0;
        var cutting = rising && movement.jumpCutArmed && !movement.jumpHeld;
        
        var g = rising
            ? tuning.JumpRiseGravity * (cutting ? tuning.JumpCutGravityScale : 1)
            : tuning.JumpRiseGravity * tuning.FallMultipler;
        
        // No apex when cut, we want it to land ASAP
        if (rising && !cutting && velocity.Z < tuning.JumpApexThreshold) g *= tuning.JumpApexGravityScale;

        velocity.Z = Math.Max(-tuning.TerminalVelocity, velocity.Z - g * deltaTime);
    }
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
    static bool TouchingSteepSlope(ref CharacterMovement movement, out Vector3d n) {
        n = movement.steepNormal;
        return movement.touchedSteep;
    }

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
        var speed01 = Utils.Clamp01(movement.ActualHorizontalSpeed / tuning.MaxSpeed);
        var maxStep = tuning.TurnSpeed * double.Lerp(1, 0.45, speed01) * deltaTime;

        var delta = Utils.WrapAngle(targetYaw - movement.Yaw);
        movement.Yaw = Utils.WrapAngle(movement.Yaw + double.Clamp(delta, -maxStep, maxStep));
    }
    void UpdateVisualYaw(ref CharacterMovement movement, float deltaTime) {
        var previous = movement.VisualYaw;
        var d = Utils.WrapAngle(movement.Yaw - movement.VisualYaw);
        movement.VisualYaw = Utils.WrapAngle(movement.VisualYaw + d * (1 - Utils.Exp2(-deltaTime / 0.05f)));
        movement.TurnRate = Utils.WrapAngle(movement.VisualYaw - previous) / deltaTime;
    }
    static Vector3d Facing(ref CharacterMovement movement) => new Vector3d(Math.Sin(movement.Yaw), Math.Cos(movement.Yaw), 0);
    
    void ProbeGround(World world, Entity entity, ref CharacterMovement movement, float deltaTime) {
        var wasGrounded = movement.Grounded;
        movement.Grounded = false;
        movement.GroundNormal = Vector3d.UnitZ;
        movement.touchedSteep = false;
        groundEntity = Entity.Null;
        
        ref var position = ref movement.Position;
        ref var velocity = ref movement.Velocity;
        
        // Do not check ground at the start of a jump
        if (movement.jumpLockTicks > 0) {
            movement.jumpLockTicks--;
        }
        // A step up already swept the surface under the feet, so it is authoritative for this tick
        else if (stepUpsThisTick > 0) {
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
                groundEntity = ground.Entity;

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
            else if (depenetrationGrounded) {
                // A rising platform can cross our feet between two ticks: our own sweep is sized on
                // our fall, not on how fast the world came to us. Depenetration already answered.
                movement.Grounded = true;
                movement.GroundNormal = depenetrationNormal;
                groundEntity = depenetrationEntity;

                if (!wasGrounded) {
                    OnLanded(world, entity, ref movement, -velocity.Z);
                }

                if (velocity.Z < 0) velocity.Z = 0;
            }
            else if (overlapped || stepUpGrace > 0) {
                // Overlapping, or mid-climb on a nose too steep to read as ground: hold the state and
                // let depenetration sort the position out.
                movement.Grounded = wasGrounded;
                if (movement.Grounded && velocity.Z < 0) velocity.Z = 0;
            }
        }

        // Smooth normal
        var smoothFactor = 1 - Utils.Exp2(-deltaTime / 0.05f);
        groundNormalSmoothed = Vector3d.Lerp(groundNormalSmoothed, movement.GroundNormal, smoothFactor).Normalized();
    }
    double GroundProbeDistance(float deltaTime) {
        return Math.Max(
            tuning.StepDownHeight,
            1.5f * tuning.MaxSpeed * Math.Tan(tuning.WalkableAngle) * deltaTime + CharacterShape.SkinWidth);
    }
    bool Walkable(bool grounded, Vector3d n) {
        return n.Z >= (grounded ? tuning.UnwalkableCos : tuning.WalkableCos);
    }

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
        movement.Yaw = Utils.WrapAngle(movement.Yaw + MovingPlatformYawDelta(ridingPlatform));
        
        movement.platformVelocity = achieved / deltaTime;
        movement.platformMemory = tuning.CoyoteTime;
    }
    Vector3d SweepStop(Vector3d position, Vector3d motion, PhysicsLayer layer) {
        var d = motion.Length();
        if (d < Utils.Epsilon) return position;

        var dir = motion / d;
        var (shape, offset) = GetQueryShape();
        var sweepOrigin = position + offset;
        var sweepMotion = dir * (d + CharacterShape.SkinWidth);
        var sweepHit =
            simulation.Sweep(shape, (Vector3)sweepOrigin, (Vector3)sweepMotion, out var h, layer)
            && ValidHit(h);

        return sweepHit ? position + dir * Math.Max(h.Distance - CharacterShape.SkinWidth, 0) : position + motion;
    }
    void CheckCrush() {
        if (crushedThisTick || stuckTicks > CrushStuckTicks) {
            OnCrushed();
        }
    }
    static Vector3d MovingPlatformDelta(in MovingPlatform platform, Vector3d point) {
        var inv = platform.PreviousTransform.Inverse();
        var local = Vector3d.Transform(point, inv);
        return Vector3d.Transform(local, platform.CurrentTransform) - point;
    }
    static double MovingPlatformYawDelta(in MovingPlatform platform) {
        return ExtractYaw(platform.CurrentTransform) - ExtractYaw(platform.PreviousTransform);
    }
    
    void UpdateAnimParams(ref CharacterMovement movement, ref CharacterAnimParams anim, float deltaTime) {
        var horizontalSpeed = movement.ActualHorizontalSpeed;
        var carryHorizontalSpeed = Utils.FlattenXY(movement.carryVelocity).Length();
        
        anim.Grounded = movement.Grounded;
        anim.Speed01 = Utils.Clamp01((horizontalSpeed - carryHorizontalSpeed) / tuning.MaxSpeed);
        anim.VerticalSpeed = movement.Grounded ? 0 : movement.Velocity.Z;
        anim.TurnRate = movement.TurnRate;
        anim.SlopeAngle = Math.Acos(Math.Clamp(groundNormalSmoothed.Z, -1, 1));

        // State + time in state
        var state = (movement.Grounded || movement.coyoteTimer > 0) ? CharacterMoveState.Grounded
            : movement.touchedSteep ? CharacterMoveState.Sliding
            : CharacterMoveState.Airborne;

        if (state != anim.State) {
            anim.State = state;
            anim.TimeInState = 0;
        }
        else {
            anim.TimeInState += deltaTime;
        }
    }
    
    // Helpers

    static bool ValidHit(in RigidBodyHit hit) => hit.Normal.LengthSquared() > 0.5;
    static double ExtractYaw(in Matrix4x4d mtx) => Math.Atan2(mtx.M21, mtx.M22);
    
    // Events

    void OnJumpStarted(World world, Entity entity, ref CharacterMovement movement) {
        if (movement.platformMemory > 0) {
            movement.carryVelocity = Utils.ClampLength(Utils.FlattenXY(movement.platformVelocity), tuning.MaxInheritedSpeed);
            movement.Velocity.Z += Math.Clamp(movement.platformVelocity.Z, -tuning.MaxInheritedRise, tuning.MaxInheritedRise);
        }
        
        world.Events<CharacterEvents.Jumped>().Write(new CharacterEvents.Jumped(entity));
    }
    void OnLanded(World world, Entity entity, ref CharacterMovement movement, double impactSpeed) {
        movement.jumpCutArmed = false;
        movement.cornerCorrectUsed = false;
        movement.carryVelocity = Vector3d.Zero;
        world.Events<CharacterEvents.Landed>().Write(new CharacterEvents.Landed(entity, impactSpeed));
    }
    void OnCrushed() {
        Console.WriteLine($"Warning: Character crushed");
    }
    
    // Types

    record struct Contact(Vector3d Normal, Vector3d Position, Entity Entity, bool VerticalPass);
}