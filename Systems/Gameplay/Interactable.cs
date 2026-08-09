using AdventureGame.Systems.CharacterController;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Systems;

// Something the character can act on: a point offset from its object, with a reach. No volume, so it
// rides any entity, still or moving, and costs one modifier to author. Using it emits `Emit` on the
// signal bus; what answers is the bus's business, exactly as for a button.
struct Interactable() {
    public string Prompt = "";
    public string Emit = "";
    public Vector3d Offset;       // where the hand lands, from the entity's origin, turning with it
    public double Reach;
    public bool Once;             // retires for good once used, remembered outside the world
    public bool Enabled = true;   // orthogonal pause: a sequence can silence it and hand it back
}

// Rides whoever can use things, and holds what he has in reach right now.
struct Interactor {
    public Entity Candidate;
}

sealed class InteractionSystem(IInput input, Flags flags, PlayerControl control) : ISystem {
    // Cosine of half the 120 degree cone in front of the character, and how far up or down reach carries.
    const double ConeCos = 0.5;
    const double VerticalReach = 2;

    // A spent one-shot is remembered as a flag rather than on the component: the level is rebuilt from
    // its file on every reload, and an emptied chest must not refill itself.
    public static string UsedFlag(string emit) => emit + ".used";

    public void Update(World world, EntityCommands commands, float deltaTime) {
        // Consume takes the press edge exactly once. A sequence holding the reins drains it all the
        // same, so a press made mid-scene cannot fire the instant control comes back.
        var used = input.Consume(Controls.Interact) && !control.Locked;

        foreach (var row in world.Query<CharacterMovement, Interactor>()) {
            ref var interactor = ref row.Component2;
            interactor.Candidate = control.Locked ? Entity.Null : Select(world, row.Component1);

            if (!used || interactor.Candidate.IsNull)
                continue;

            var interactable = world.Get<Interactable>(interactor.Candidate);
            if (interactable.Once)
                flags.Set(UsedFlag(interactable.Emit));
            world.Events<Signal>().Write(new Signal(interactable.Emit, interactor.Candidate));
        }
    }

    // Best thing within its own reach and inside the cone the character faces, scored by how deep into
    // that reach he stands - so a small thing he is on top of wins over a wide one further out.
    Entity Select(World world, in CharacterMovement movement) {
        var facing = new Vector3d(Math.Sin(movement.Yaw), Math.Cos(movement.Yaw), 0);
        var best = Entity.Null;
        var bestScore = double.MaxValue;

        foreach (var row in world.Query<Interactable, RelativeTransform>()) {
            var interactable = row.Component1;
            if (!interactable.Enabled || interactable.Once && flags.Has(UsedFlag(interactable.Emit)))
                continue;

            var transform = row.Component2.LocalTransform;
            var point = transform.Position + Vector3d.Transform(interactable.Offset, transform.Rotation);
            var toPoint = point - movement.Position;
            if (Math.Abs(toPoint.Z) > VerticalReach)
                continue;

            var flat = Utils.FlattenXY(toPoint);
            var distance = flat.Length();
            if (distance > interactable.Reach)
                continue;
            // Standing right on it has no direction to judge, so the cone only applies at a distance
            if (distance > Utils.Epsilon && Vector3d.Dot(flat / distance, facing) < ConeCos)
                continue;

            var score = distance / interactable.Reach;
            if (score >= bestScore)
                continue;
            bestScore = score;
            best = row.Entity;
        }

        return best;
    }
}
