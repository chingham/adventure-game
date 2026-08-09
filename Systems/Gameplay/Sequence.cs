using AdventureGame.Systems.CharacterController;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Systems;

// One step of a sequence. The verb picks which fields matter, and branches carry nested lists - so a
// step is plain immutable data, shared by every runner and readable straight from the level file.
// Adding a verb means one case in Run and one line in Validate, nothing else.
sealed record Step {
    public string Do { get; init; } = "";
    public float S { get; init; }            // seconds, for wait and move
    public string? Signal { get; init; }
    public string? Flag { get; init; }
    public string? Id { get; init; }
    public string? Text { get; init; }
    public float[]? By { get; init; }        // move offset
    public Step[]? Then { get; init; }
    public Step[]? Else { get; init; }

    public string? Validate() => Do.ToLowerInvariant() switch {
        "wait" => S > 0 ? null : "wait needs a positive s",
        "emit" or "waitsignal" => Signal is not null ? null : $"{Do} needs signal",
        "if" => Flag is null ? "if needs flag" : Branch(Then) ?? Branch(Else),
        "gate" or "release" => null,
        "face" => null,
        "set" or "clear" => Flag is not null ? null : $"{Do} needs flag",
        "enable" or "disable" => Id is not null ? null : $"{Do} needs id",
        "toast" => Text is not null ? null : "toast needs text",
        "move" => Id is null ? "move needs id"
            : By is not { Length: 3 } ? "move needs by [x, y, z]"
            : S > 0 ? null : "move needs a positive s",
        _ => $"unknown step '{Do}'"
    };

    static string? Branch(Step[]? steps) {
        foreach (var step in steps ?? [])
            if (step.Validate() is { } error)
                return error;
        return null;
    }

    // Objects this step and its branches point at, so the file can check they resolve.
    public IEnumerable<string> References() {
        if (Id is { } id && Do.ToLowerInvariant() is "enable" or "disable" or "move")
            yield return id;
        foreach (var step in Then ?? [])
            foreach (var reference in step.References())
                yield return reference;
        foreach (var step in Else ?? [])
            foreach (var reference in step.References())
                yield return reference;
    }
}

// A rule with no place in the world: hearing `On` runs these steps.
struct SequenceDefinition {
    public string On;
    public Step[] Steps;
}

// A sequence in flight. `Pending` is what is left to run, so a branch just splices its steps to the
// front and nesting needs no stack of its own.
struct SequenceRunner {
    public string On;
    public Entity Source;          // whatever emitted the signal, for `face` and for the report back
    public List<Step> Pending;
    public int Gates;              // holds on the player this runner is responsible for

    internal double timer;
    internal Vector3d moveFrom;
    internal bool moving;
}

// What a step leaves behind: it is finished with, it wants the frame again, or it rewrote what comes
// next and the front must be read again.
enum StepOutcome { Done, Holding, Rewrote }

sealed class SequenceSystem(Flags flags, PlayerControl control, Toasts toasts) : ISystem {
    readonly EventReader<Signal> signals = new();
    readonly List<Signal> heard = [];

    public void Update(World world, EntityCommands commands, float deltaTime) {
        heard.Clear();
        foreach (var signal in signals.Read(world))
            heard.Add(signal);

        Start(world, commands);
        Advance(world, commands, deltaTime);

        // Recomputed from the runners themselves rather than counted up and down: a runner that dies
        // mid-sequence - a hot reload, say - can then never leave the player frozen.
        control.Locked = false;
        foreach (var row in world.Query<SequenceRunner>())
            control.Locked |= row.Component1.Gates > 0;
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
                }).With(new LevelBrick());
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

    StepOutcome Run(World world, ref SequenceRunner runner, Step step, float deltaTime) {
        switch (step.Do.ToLowerInvariant()) {
            case "wait":
                runner.timer += deltaTime;
                if (runner.timer < step.S)
                    return StepOutcome.Holding;
                runner.timer = 0;
                return StepOutcome.Done;

            case "emit":
                world.Events<Signal>().Write(new Signal(step.Signal!, runner.Source));
                return StepOutcome.Done;

            case "waitsignal":
                return heard.Any(s => s.Name == step.Signal) ? StepOutcome.Done : StepOutcome.Holding;

            case "if": {
                var branch = flags.Has(step.Flag!) ? step.Then : step.Else;
                runner.Pending.RemoveAt(0);
                if (branch is { Length: > 0 })
                    runner.Pending.InsertRange(0, branch);
                return StepOutcome.Rewrote;
            }

            case "gate":
                runner.Gates++;
                return StepOutcome.Done;

            case "release":
                runner.Gates = Math.Max(0, runner.Gates - 1);
                return StepOutcome.Done;

            case "face":
                Face(world, Target(world, step.Id, runner.Source));
                return StepOutcome.Done;

            case "set":
                flags.Set(step.Flag!);
                return StepOutcome.Done;

            case "clear":
                flags.Clear(step.Flag!);
                return StepOutcome.Done;

            case "enable":
            case "disable":
                Switch(world, Target(world, step.Id, Entity.Null), step.Do.ToLowerInvariant() == "enable");
                return StepOutcome.Done;

            case "toast":
                toasts.Show(step.Text!);
                return StepOutcome.Done;

            case "move":
                return Move(world, ref runner, step, deltaTime);

            default:
                return StepOutcome.Done;
        }
    }

    // Steps

    // Slides an object by an offset, eased. Written straight to the transform: the body sync reads it
    // on the next tick, so a static slab carries its collider along.
    StepOutcome Move(World world, ref SequenceRunner runner, Step step, float deltaTime) {
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
        var progress = Utils.SmoothStep01(runner.timer / step.S);
        var by = new Vector3d(step.By![0], step.By[1], step.By[2]);
        transform.LocalTransform.Position = runner.moveFrom + by * progress;

        if (runner.timer < step.S)
            return StepOutcome.Holding;

        runner.moving = false;
        runner.timer = 0;
        return StepOutcome.Done;
    }

    static void Face(World world, Entity target) {
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
