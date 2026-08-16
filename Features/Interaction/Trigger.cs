using AdventureGame.Features.Character;
using AdventureGame.Features.Sequences;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;

namespace AdventureGame.Features.Interaction;

// Overlap volume wired to the signal bus: it announces who comes and goes without knowing what listens.
// Rearm latches it after an entry until the named signal comes home, so a volume that summons something
// cannot summon it twice while it is still on its way.
struct Trigger() {
    public string? Enter;
    public string? Exit;
    public string? Rearm;
    public bool Enabled = true;   // orthogonal pause: a sequence can silence the volume and hand it back
    internal bool Latched;
}

sealed class TriggerSystem : ISystem {
    readonly EventReader<TriggerEvent> triggers = new();
    readonly EventReader<Signal> signals = new();

    public void Update(World world, EntityCommands commands, float deltaTime) {
        foreach (var evt in triggers.Read(world)) {
            if (evt.Kind == TriggerEventKind.Stay || !world.Has<CharacterMovement>(evt.Other))
                continue;
            if (!world.Has<Trigger>(evt.Trigger))
                continue;

            ref var trigger = ref world.Get<Trigger>(evt.Trigger);
            if (!trigger.Enabled)
                continue;
            if (evt.Kind == TriggerEventKind.Exit) {
                Emit(world, trigger.Exit, evt.Trigger);
                continue;
            }
            if (trigger.Latched)
                continue;

            trigger.Latched = trigger.Rearm is not null;
            Emit(world, trigger.Enter, evt.Trigger);
        }

        // Release: whatever the volume was waiting on has come home
        foreach (var signal in signals.Read(world))
            foreach (var row in world.Query<Trigger>())
                if (signal.Name == row.Component1.Rearm)
                    row.Component1.Latched = false;
    }

    static void Emit(World world, string? name, Entity source) {
        if (name is not null)
            world.Events<Signal>().Write(new Signal(name, source));
    }
}
