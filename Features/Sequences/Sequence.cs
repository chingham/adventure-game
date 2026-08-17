using AdventureGame.Common;
using AdventureGame.Features.Character;
using AdventureGame.Features.Interaction;
using AdventureGame.Features.Progression;
using AdventureGame.Flow;
using AdventureGame.Level;
using AdventureGame.Presentation;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Features.Sequences;

// A rule with no place in the world: hearing `On` runs these steps.
struct SequenceDefinition {
    public string On;
    public IStep[] Steps;
}

// A sequence in flight. `Pending` is what is left to run, so a branch just splices its steps to the
// front and nesting needs no stack of its own.
struct SequenceRunner {
    public string On;
    public Entity Source;          // whatever emitted the signal, for `face` and for the report back
    public List<IStep> Pending;
    public int Gates;              // holds on the player this runner is responsible for

    internal double timer;
    internal Vector3d moveFrom;
    internal bool moving;
}

// What a step leaves behind: it is finished with, it wants the frame again, or it rewrote what comes
// next and the front must be read again.
enum StepOutcome { Done, Holding, Rewrote }

sealed class SequenceSystem(Flags flags, GameFlow flow, Toasts toasts) : ISystem {
    readonly EventReader<Signal> signals = new();
    readonly List<Signal> heard = [];

    IDisposable? controlLock = null;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        heard.Clear();
        foreach (var signal in signals.Read(world))
            heard.Add(signal);

        Start(world, commands);
        Advance(world, commands, deltaTime);

        // Recomputed from the runners themselves rather than counted up and down: a runner that dies
        // mid-sequence - a hot reload, say - can then never leave the player frozen.
        var locked = false;
        foreach (var row in world.Query<SequenceRunner>())
            locked |= row.Component1.Gates > 0;
        
        if (locked && controlLock is null)
            controlLock = flow.LockPlayerControl();
        else if (!locked && controlLock is not null) {
            controlLock.Dispose();
            controlLock = null;
        }
    }

    // Starting

    void Start(World world, EntityCommands commands) {
        foreach (var signal in heard)
            foreach (var row in world.Query<SequenceDefinition>()) {
                var definition = row.Component1;
                if (definition.On != signal.Name || Running(world, definition.On))
                    continue;

                commands.Spawn(new SequenceRunner {
                    On = definition.On,
                    Source = signal.Source,
                    Pending = [..definition.Steps]
                });
            }
    }

    // One runner at a time per signal: pressing twice while it plays must not stack two of them.
    static bool Running(World world, string on) {
        foreach (var row in world.Query<SequenceRunner>())
            if (row.Component1.On == on)
                return true;
        return false;
    }

    // Running

    void Advance(World world, EntityCommands commands, float deltaTime) {
        foreach (var row in world.Query<SequenceRunner>()) {
            ref var runner = ref row.Component1;

            while (runner.Pending.Count > 0) {
                var outcome = Run(world, ref runner, runner.Pending[0], deltaTime);
                if (outcome == StepOutcome.Holding)
                    break;
                if (outcome == StepOutcome.Done)
                    runner.Pending.RemoveAt(0);
            }

            if (runner.Pending.Count > 0)
                continue;

            // Finishing reports home, which is what a rearmed trigger or a button waits for
            runner.Gates = 0;
            world.Events<Signal>().Write(new Signal(runner.On + ".done", runner.Source));
            commands.Destroy(row.Entity);
        }
    }

    StepOutcome Run(World world, ref SequenceRunner runner, IStep step, float deltaTime) {
        switch (step) {
            case Wait wait:
                runner.timer += deltaTime;
                if (runner.timer < wait.S)
                    return StepOutcome.Holding;
                runner.timer = 0;
                return StepOutcome.Done;

            case Emit emit:
                world.Events<Signal>().Write(new Signal(emit.Signal, runner.Source));
                return StepOutcome.Done;

            case WaitSignal wait:
                return heard.Any(s => s.Name == wait.Signal) ? StepOutcome.Done : StepOutcome.Holding;

            case If branch: {
                var taken = flags.Has(branch.Flag) ? branch.Then : branch.Else;
                runner.Pending.RemoveAt(0);
                if (taken is { Length: > 0 })
                    runner.Pending.InsertRange(0, taken);
                return StepOutcome.Rewrote;
            }

            case Gate:
                runner.Gates++;
                return StepOutcome.Done;

            case Release:
                runner.Gates = Math.Max(0, runner.Gates - 1);
                return StepOutcome.Done;

            case Face face:
                LookAt(world, Target(world, face.Id, runner.Source));
                return StepOutcome.Done;

            case SetFlag set:
                flags.Set(set.Flag);
                return StepOutcome.Done;

            case ClearFlag clear:
                flags.Clear(clear.Flag);
                return StepOutcome.Done;

            case Enable enable:
                Switch(world, Target(world, enable.Id, Entity.Null), enabled: true);
                return StepOutcome.Done;

            case Disable disable:
                Switch(world, Target(world, disable.Id, Entity.Null), enabled: false);
                return StepOutcome.Done;

            case Toast toast:
                toasts.Show(toast.Text);
                return StepOutcome.Done;

            case Move move:
                return Slide(world, ref runner, move, deltaTime);

            default:
                return StepOutcome.Done;
        }
    }

    // Steps

    // Slides an object by an offset, eased. Written straight to the transform: the body sync reads it
    // on the next tick, so a static slab carries its collider along.
    StepOutcome Slide(World world, ref SequenceRunner runner, Move step, float deltaTime) {
        var target = Target(world, step.Id, Entity.Null);
        if (target.IsNull || !world.Has<RelativeTransform>(target))
            return StepOutcome.Done;

        ref var transform = ref world.Get<RelativeTransform>(target);
        if (!runner.moving) {
            runner.moveFrom = transform.LocalTransform.Position;
            runner.moving = true;
            runner.timer = 0;
        }

        runner.timer += deltaTime;
        var progress = Ease.Smooth.Evaluate((float)(runner.timer / step.S));
        transform.LocalTransform.Position = runner.moveFrom + step.By * progress;

        if (runner.timer < step.S)
            return StepOutcome.Holding;

        runner.moving = false;
        runner.timer = 0;
        return StepOutcome.Done;
    }

    static void LookAt(World world, Entity target) {
        if (target.IsNull || !world.Has<RelativeTransform>(target))
            return;

        var at = world.Get<RelativeTransform>(target).LocalTransform.Position;
        foreach (var row in world.Query<CharacterMovement>()) {
            ref var movement = ref row.Component1;
            if (Utils.TryFlatDir(at - movement.Position, out var direction))
                movement.Yaw = Math.Atan2(direction.X, direction.Y);
        }
    }

    // Whichever of the two an object carries. Toggling also unlatches a volume, so it comes back armed
    // rather than stuck on whatever it was waiting for when it was silenced.
    static void Switch(World world, Entity target, bool enabled) {
        if (target.IsNull)
            return;

        if (world.Has<Interactable>(target)) {
            ref var interactable = ref world.Get<Interactable>(target);
            interactable.Enabled = enabled;
        }

        if (world.Has<Trigger>(target)) {
            ref var trigger = ref world.Get<Trigger>(target);
            trigger.Enabled = enabled;
            trigger.Latched = false;
        }
    }

    // Helpers

    static Entity Target(World world, string? id, Entity fallback) =>
        id is null ? fallback : Find(world, id);

    static Entity Find(World world, string id) {
        foreach (var row in world.Query<Name>())
            if (row.Component1.Value == id)
                return row.Entity;
        return Entity.Null;
    }
}
