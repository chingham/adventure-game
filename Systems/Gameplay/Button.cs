using AdventureGame.Systems.CharacterController;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Systems;

// A button is two entities: a solid pad (the visual, it sinks) and a trigger volume just above it,
// which carries this component. Stepping in emits the signal and latches the button; it pops back
// when "<emit>.done" comes home.
struct Button {
    public string Emit;
    public Entity Pad;
    public double RestZ;          // pad centre altitude when released
    public bool Latched;
    internal double Depression;   // smoothed 0..1, drives the sink
}

sealed class ButtonSystem : ISystem {
    const double SinkDepth = 0.15;
    const double SinkDecay = 18;

    readonly EventReader<TriggerEvent> triggers = new();
    readonly EventReader<Signal> signals = new();

    public void Update(World world, EntityCommands commands, float deltaTime) {
        // Press: the character stepped into a button volume
        foreach (var evt in triggers.Read(world)) {
            if (evt.Kind != TriggerEventKind.Enter || !world.Has<CharacterMovement>(evt.Other))
                continue;
            if (!world.Has<Button>(evt.Trigger))
                continue;

            ref var button = ref world.Get<Button>(evt.Trigger);
            if (button.Latched)
                continue;
            button.Latched = true;
            world.Events<Signal>().Write(new Signal(button.Emit));
        }

        // Release: the summoned device completed its cycle
        foreach (var signal in signals.Read(world))
            foreach (var row in world.Query<Button>())
                if (signal.Name == row.Component1.Emit + ".done")
                    row.Component1.Latched = false;

        // Visual: ease the pad toward pressed or rest altitude
        foreach (var row in world.Query<Button>()) {
            ref var button = ref row.Component1;
            button.Depression = Decay.ExpDecay(button.Depression, button.Latched ? 1 : 0, SinkDecay, deltaTime);

            ref var pad = ref world.Get<RelativeTransform>(button.Pad);
            var position = pad.LocalTransform.Position;
            pad.LocalTransform.Position = new Vector3d(position.X, position.Y, button.RestZ - button.Depression * SinkDepth);
        }
    }
}
