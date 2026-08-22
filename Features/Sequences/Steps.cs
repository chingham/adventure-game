using AdventureGame.Common;
using AdventureGame.Features.Character;
using AdventureGame.Features.Dialogue;
using AdventureGame.Features.Interaction;
using AdventureGame.Features.Progression;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Features.Sequences;

// Time

record struct Wait(float S) : IStep {
    public string? Validate() => S > 0 ? null : "wait needs a positive s";

    public IStepRun? Start(SequenceRun ctx) => new Run(this);

    sealed class Run(Wait wait) : IStepRun {
        float timer;
        
        public bool Update(SequenceRun ctx, float deltaTime) {
            timer += deltaTime;
            return timer >= wait.S;
        }
    }
}

// The signal bus

record struct Emit(string Signal) : IStep {
    public string? Validate() => Signal is not null ? null : "emit needs a signal";

    public IStepRun? Start(SequenceRun ctx) {
        ctx.World.Events<Signal>().Write(new Signal(Signal, ctx.Source));
        return null;
    }
}

record struct WaitSignal(string Signal) : IStep {
    public string? Validate() => Signal is not null ? null : "waitSignal needs a signal";

    public IStepRun? Start(SequenceRun ctx) => new Run(this);

    sealed class Run(WaitSignal signal) : IStepRun {
        public bool Update(SequenceRun ctx, float deltaTime) {
            return ctx.Heard.Any(s => s.Name == signal.Signal);
        }
    }
}

// The player's reins. A gate holds them until its release, and the runner drops them all when it ends.

record struct Gate : IStep {
    public IStepRun? Start(SequenceRun ctx) {
        ctx.Gates++;
        return null;
    }
}

record struct Release : IStep {
    public IStepRun? Start(SequenceRun ctx) {
        ctx.Gates = Math.Max(0, ctx.Gates - 1);
        return null;
    }
}

// Turns the character toward an object, or toward whatever emitted the signal when it names none
record struct Face(string? Id) : IStep {
    public IStepRun? Start(SequenceRun ctx) {
        var target = Id is null ? ctx.Source : ctx.Find(Id);
        if (target.IsNull || !ctx.World.Has<RelativeTransform>(target))
            return null;

        var at = ctx.World.Get<RelativeTransform>(target).LocalTransform.Position;
        foreach (var row in ctx.World.Query<CharacterMovement>()) {
            ref var movement = ref row.Component1;
            if (Utils.TryFlatDir(at - movement.Position, out var direction))
                movement.Yaw = Math.Atan2(direction.X, direction.Y);
        }

        return null;
    }
}

// What the world remembers

record struct SetFlag(string Flag) : IStep {
    public string? Validate() => Flag is not null ? null : "set needs a flag";
    
    public IStepRun? Start(SequenceRun ctx) {
        var flags = ctx.Get<Flags>();
        flags.Set(Flag);
        return null;
    }
}

record struct ClearFlag(string Flag) : IStep {
    public string? Validate() => Flag is not null ? null : "clear needs a flag";
    
    public IStepRun? Start(SequenceRun ctx) {
        var flags = ctx.Get<Flags>();
        flags.Clear(Flag);
        return null;
    }
}

// Objects

record struct Enable(string Id) : IStep {
    public string? Validate() => Id is not null ? null : "enable needs an id";
    public IEnumerable<string> References() => [Id];
    
    internal static void Switch(SequenceRun ctx, string id, bool enabled) {
        var target = ctx.Find(id);
        if (target.IsNull) return;

        if (ctx.World.Has<Interactable>(target)) {
            ref var interactable = ref ctx.World.Get<Interactable>(target);
            interactable.Enabled = enabled;
        }

        if (ctx.World.Has<Trigger>(target)) {
            ref var trigger = ref ctx.World.Get<Trigger>(target);
            trigger.Enabled = enabled;
            trigger.Latched = false;
        }
    }
    
    public IStepRun? Start(SequenceRun ctx) {
        Switch(ctx, Id, enabled: true);
        return null;
    }
}

record struct Disable(string Id) : IStep {
    public string? Validate() => Id is not null ? null : "disable needs an id";
    public IEnumerable<string> References() => [Id];
    
    public IStepRun? Start(SequenceRun ctx) {
        Enable.Switch(ctx, Id, enabled: false);
        return null;
    }
}

record struct Move(string Id, Vector3d By, float S) : IStep {
    public string? Validate() =>
        Id is null ? "move needs an id"
        : S > 0 ? null : "move needs a positive s";

    public IEnumerable<string> References() => [Id];
    
    public IStepRun? Start(SequenceRun ctx) => new Run(this);
    
    sealed class Run(Move move) : IStepRun {
        bool moving;
        float timer;
        Vector3d from;

        public bool Update(SequenceRun ctx, float deltaTime) {
            var target = ctx.Find(move.Id);
            if (target.IsNull || !ctx.World.Has<RelativeTransform>(target))
                return true;

            ref var transform = ref ctx.World.Get<RelativeTransform>(target);
            if (!moving) {
                from = transform.LocalTransform.Position;
                moving = true;
                timer = 0;
            }

            timer += deltaTime;
            var progress = Ease.Smooth.Evaluate(timer / move.S);
            transform.LocalTransform.Position = from + move.By * progress;

            if (timer < move.S)
                return false;

            moving = false;
            timer = 0;
            return true;
        }
    }
}

// Dialogue

record struct Say(string Speaker, string[] Pages) : IStep {
    public string? Validate() {
        if (Speaker is null) return "say needs a speaker";
        if (Pages is null || Pages.Length == 0) return "say needs pages";
        return null;
    }
    
    public IStepRun? Start(SequenceRun ctx) => new Run(this);

    sealed class Run(Say say) : IStepRun {
        string? conversationId;
        
        public bool Update(SequenceRun ctx, float deltaTime) {
            var speech = ctx.Get<Speech>();
            
            if (conversationId is null) {
                conversationId = speech.Begin(say.Speaker, say.Pages);
                if (conversationId is null) return false;
                
                ctx.Gates++;
            }

            if (speech.ActiveConversation?.Id == conversationId) return false;

            ctx.Gates--;
            return true;
        }
    }
}

record struct Bark(string Id, string Text) : IStep {
    public string? Validate() {
        if (Id is null) return "bark needs an id";
        if (Text is null) return "bark needs a text";
        return null;
    }
    
    public IStepRun? Start(SequenceRun ctx) {
        Console.WriteLine($"TODO: Bark {Id}: {Text}");
        return null;
    }
}

record struct Notice(string Text) : IStep {
    public string? Validate() => Text is not null ? null : "notice needs a text";
    
    public IStepRun? Start(SequenceRun ctx) {
        Console.WriteLine($"TODO: Notice: {Text}");
        return null;
    }
}

// Branching

record struct If(string Flag, IStep[] Then, IStep[] Else) : IStep {
    public string? Validate() =>
        Flag is null ? "if needs a flag"
        : Branch(Then) ?? Branch(Else);

    public IEnumerable<string> References() =>
        (Then ?? []).Concat(Else ?? []).SelectMany(step => step.References());

    static string? Branch(IStep[]? steps) {
        foreach (var step in steps ?? [])
            if (step.Validate() is { } error)
                return error;
        return null;
    }
    
    public IStepRun? Start(SequenceRun ctx) {
        var flags = ctx.Get<Flags>();
        var taken = flags.Has(Flag) ? Then : Else;
        if (taken is { Length: > 0 })
            ctx.Pending.InsertRange(0, taken);
        return null;
    }
}
