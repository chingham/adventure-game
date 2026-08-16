using AdventureGame.Features.Interaction;
using AdventureGame.Features.Sequences;
using Quark.Ecs;

namespace AdventureGame.Debug;

// Stands in for the game UI: says out loud what has come into reach, and what travels on the signal bus
// once it is used. Retire it the day prompts are drawn on screen.
sealed class InteractionProbeSystem : ISystem {
    readonly EventReader<Signal> signals = new();
    Entity announced = Entity.Null;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        foreach (var row in world.Query<Interactor>()) {
            var candidate = row.Component1.Candidate;
            if (candidate == announced)
                continue;
            announced = candidate;

            Console.WriteLine(candidate.IsNull
                ? "[Interact] nothing in reach"
                : $"[Interact] {world.Get<Interactable>(candidate).Prompt}");
        }

        foreach (var signal in signals.Read(world))
            Console.WriteLine($"[Signal] {signal.Name}");
    }
}
