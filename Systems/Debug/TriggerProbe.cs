using AdventureGame.Systems.CharacterController;
using Quark.Ecs;
using Quark.Kit.Components;

namespace AdventureGame.Systems;

// Scaffolding: logs which zone the character enters and leaves. Every gameplay feature reads the same
// TriggerEvent channel and filters on its own component, so none of them has to touch the controller.
sealed class TriggerProbeSystem : ISystem {
    readonly EventReader<TriggerEvent> triggers = new();

    public void Update(World world, EntityCommands commands, float deltaTime) {
        foreach (var evt in triggers.Read(world)) {
            if (evt.Kind == TriggerEventKind.Stay || !world.Has<CharacterMovement>(evt.Other))
                continue;

            Console.WriteLine($"[Trigger] {evt.Kind} '{ZoneName(world, evt.Trigger)}'");
        }
    }

    static string ZoneName(World world, Entity zone) =>
        world.TryGet<Name>(zone, out var name) ? name.Value : $"#{zone.Index}";
}
