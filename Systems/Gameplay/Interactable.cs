using AdventureGame.Systems.CharacterController;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Systems;

// Something the character can act on. Purely spatial - a point with a reach, no trigger volume - so it
// rides any entity, still or moving, and costs two lines to author. Using it emits `Emit` on the signal
// bus; what answers is the bus's business, exactly as for a button.
struct Interactable() {
    public string Prompt = "";
    public string Emit = "";
    public double Reach;
    public bool Once;             // retires the moment it is used; a sequence can also disable it
    public bool Enabled = true;
}

// Rides whoever can use things, and holds what he has in reach right now.
struct Interactor {
    public Entity Candidate;
}

sealed class InteractionSystem(IInput input) : ISystem {
    // Cosine of half the 120 degree cone in front of the character, and how far up or down reach carries.
    const double ConeCos = 0.5;
    const double VerticalReach = 2;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        // Consume takes the press edge exactly once, whoever is first to ask
        var used = input.Consume(Controls.Interact);

        foreach (var row in world.Query<CharacterMovement, Interactor>()) {
            ref var interactor = ref row.Component2;
            interactor.Candidate = Select(world, row.Component1);

            if (!used || interactor.Candidate.IsNull)
                continue;

            ref var interactable = ref world.Get<Interactable>(interactor.Candidate);
            interactable.Enabled = !interactable.Once;
            world.Events<Signal>().Write(new Signal(interactable.Emit));
        }
    }

    // Best thing within its own reach and inside the cone the character faces, scored by how deep into
    // that reach he stands - so a small thing he is on top of wins over a wide one further out.
    static Entity Select(World world, in CharacterMovement movement) {
        var facing = new Vector3d(Math.Sin(movement.Yaw), Math.Cos(movement.Yaw), 0);
        var best = Entity.Null;
        var bestScore = double.MaxValue;

        foreach (var row in world.Query<Interactable, RelativeTransform>()) {
            var interactable = row.Component1;
            if (!interactable.Enabled)
                continue;

            var offset = row.Component2.LocalTransform.Position - movement.Position;
            if (Math.Abs(offset.Z) > VerticalReach)
                continue;

            var flat = Utils.FlattenXY(offset);
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
