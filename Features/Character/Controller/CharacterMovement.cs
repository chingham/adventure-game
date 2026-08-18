using System.Numerics;
using AdventureGame.App;
using AdventureGame.Common;
using Quark.Ecs;
using Quark.Kit;
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
    public LayerMask CollisionMask = Layers.Physics.Environment;

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
    //
    // Everything the solver carries from one tick to the next lives here rather than on the system,
    // so two characters cannot share one another's ground, rescue point or step-up budget.

    internal int slideIterations, exhaustedPasses;

    internal double coyoteTimer, bufferTimer;
    internal int jumpLockTicks;
    internal bool jumpCutArmed, jumpHeld;

    internal int stuckTicks;
    internal Vector3d lastValidPosition;

    internal Entity groundEntity;
    internal Vector3d groundNormalSmoothed = Vector3d.UnitZ;
    internal Vector3d platformVelocity;
    internal double platformMemory;
    internal Vector3d carryVelocity;

    internal Vector3d steepNormal;
    internal bool touchedSteep;

    internal int stepUpsThisTick;
    internal int stepUpGrace;

    internal bool cornerCorrectUsed;
}

// What only exists for the length of one tick: written by one stage, read by a later one, gone before
// the next tick starts. Keeping it out of the component is what says "this does not survive".
struct MoveTick(List<MoveContact> contacts) {
    public readonly List<MoveContact> Contacts = contacts;

    public bool Crushed;

    // Depenetration answers the ground question when the downward probe cannot: something rose into us
    public bool DepenetrationGrounded;
    public Vector3d DepenetrationNormal = Vector3d.UnitZ;
    public Entity DepenetrationEntity = Entity.Null;
}

record struct MoveContact(Vector3d Normal, Vector3d Position, Entity Entity, bool VerticalPass);

/// <summary>
/// One fixed step of the controller. The tick reads as its own summary: depenetrate, ride what carries
/// us, build a velocity, move it against the world, then find out what we are standing on. Each stage
/// lives in its own file.
/// </summary>
sealed unsafe partial class CharacterMovementSystem(RigidBodySimulation simulation, CharacterTuning tuning)
    : ISystem {
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

    // Reused rather than allocated per tick; MoveTick borrows it for the length of the step
    readonly List<MoveContact> contacts = new(8);

    public void Update(World world, EntityCommands commands, float deltaTime) {
        foreach (var row in world.Query<CharacterIntent, CharacterMovement>()) {
            var intent = row.Component1;
            ref var movement = ref row.Component2;

            UpdateCharacter(world, row.Entity, intent, ref movement, deltaTime);
        }
    }

    void UpdateCharacter(
        World world,
        Entity entity,
        CharacterIntent intent,
        ref CharacterMovement movement,
        float deltaTime) {

        var tick = new MoveTick(contacts);

        // Prepare and reset
        movement.TeleportedThisTick = false;
        movement.PreviousPosition = movement.Position;
        movement.PreviousVisualYaw = movement.VisualYaw;

        movement.stepUpsThisTick = 0;
        if (movement.stepUpGrace > 0) movement.stepUpGrace--;

        // Depenetrate
        Depenetrate(ref movement, ref tick);
        RescueIfStuck(ref movement);
        CheckCrush(ref movement, ref tick);

        // Moving platform
        ApplyPlatformDelta(world, ref movement, deltaTime);

        // Memoize start position
        var startPosition = movement.Position;

        // Update velocity
        UpdateHorizontalVelocity(ref movement, intent, deltaTime);
        UpdateVerticalVelocity(ref movement, deltaTime);
        UpdateJump(world, entity, ref movement, intent, deltaTime);

        // Facing
        UpdateFacing(ref movement, intent, deltaTime);
        UpdateVisualYaw(ref movement, deltaTime);

        // Collide and slide
        CollideAndSlide(ref movement, ref tick, deltaTime);
        movement.carryVelocity *= (-deltaTime / tuning.CarryHalfLife).Exp2();

        // Ground
        ProbeGround(world, entity, ref movement, ref tick, deltaTime);

        // Contacts
        ResolveContacts(ref movement, ref tick, startPosition, deltaTime);

        // If teleported, overwrite Previous*
        if (movement.TeleportedThisTick) {
            movement.PreviousPosition = movement.Position;
            movement.PreviousVisualYaw = movement.VisualYaw;
        }

        // Update anim params
        if (world.Has<CharacterAnimParams>(entity)) {
            ref var anim = ref world.Get<CharacterAnimParams>(entity);
            anim.ReadFrom(movement, tuning, deltaTime);
        }
    }

    // Shared queries

    static (QueryShape shape, Vector3d offset) GetQueryShape() {
        var shape = new Capsule(CharacterShape.CapsuleRadius, CharacterShape.CapsuleSegmentHeight);
        var offset = new Vector3d(0, 0, CharacterShape.CapsuleRestHeight);
        return (shape, offset);
    }

    // A sweep that stops on the first thing in the way, used wherever motion is not slid along
    Vector3d SweepStop(Vector3d position, Vector3d motion, LayerMask layer) {
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

    static bool ValidHit(in RigidBodyHit hit) => hit.Normal.LengthSquared() > 0.5;

    // Events

    void OnJumpStarted(World world, Entity entity, ref CharacterMovement movement) {
        if (movement.platformMemory > 0) {
            movement.carryVelocity =
                Vector3d.ClampLength(Utils.FlattenXY(movement.platformVelocity), tuning.MaxInheritedSpeed);
            movement.Velocity.Z +=
                Math.Clamp(movement.platformVelocity.Z, -tuning.MaxInheritedRise, tuning.MaxInheritedRise);
        }

        world.Events<CharacterEvents.Jumped>().Write(new CharacterEvents.Jumped(entity));
    }
    void OnLanded(World world, Entity entity, ref CharacterMovement movement, double impactSpeed) {
        movement.jumpCutArmed = false;
        movement.cornerCorrectUsed = false;
        movement.carryVelocity = Vector3d.Zero;
        world.Events<CharacterEvents.Landed>().Write(new CharacterEvents.Landed(entity, impactSpeed));
    }
    static void OnCrushed() {
        Console.WriteLine($"Warning: Character crushed");
    }
}
