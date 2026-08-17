using Quark.Numerics;

namespace AdventureGame.Features.Sequences;

/// <summary>
/// One beat of a sequence. Each verb is its own type carrying exactly its own fields, so a step that
/// cannot say what it needs does not compile, and the level file gets a schema per verb rather than one
/// object with every key optional.
/// <para>
/// Adding a verb is three lines: the type here, its case in <c>SequenceSystem.Run</c>, and its name in
/// <c>SequenceVocabulary</c>.
/// </para>
/// </summary>
interface IStep {
    /// <summary>What the file got wrong about this step, or null. Bound values, not raw JSON.</summary>
    string? Validate() => null;

    /// <summary>Objects this step points at by name, so a load can check they resolve.</summary>
    IEnumerable<string> References() => [];
}

// Time

record struct Wait(float S) : IStep {
    public string? Validate() => S > 0 ? null : "wait needs a positive s";
}

// The signal bus

record struct Emit(string Signal) : IStep {
    public string? Validate() => Signal is not null ? null : "emit needs a signal";
}

record struct WaitSignal(string Signal) : IStep {
    public string? Validate() => Signal is not null ? null : "waitSignal needs a signal";
}

// The player's reins. A gate holds them until its release, and the runner drops them all when it ends.

record struct Gate : IStep;

record struct Release : IStep;

// Turns the character toward an object, or toward whatever emitted the signal when it names none
record struct Face(string? Id) : IStep;

// What the world remembers

record struct SetFlag(string Flag) : IStep {
    public string? Validate() => Flag is not null ? null : "set needs a flag";
}

record struct ClearFlag(string Flag) : IStep {
    public string? Validate() => Flag is not null ? null : "clear needs a flag";
}

// Objects

record struct Enable(string Id) : IStep {
    public string? Validate() => Id is not null ? null : "enable needs an id";
    public IEnumerable<string> References() => [Id];
}

record struct Disable(string Id) : IStep {
    public string? Validate() => Id is not null ? null : "disable needs an id";
    public IEnumerable<string> References() => [Id];
}

// Slides an object by an offset, eased over s seconds
record struct Move(string Id, Vector3d By, float S) : IStep {
    public string? Validate() =>
        Id is null ? "move needs an id"
        : S > 0 ? null : "move needs a positive s";

    public IEnumerable<string> References() => [Id];
}

// Say something out loud
record struct Toast(string Text) : IStep {
    public string? Validate() => Text is not null ? null : "toast needs a text";
}

// Branching. The taken branch is spliced in front of what is left, so nesting needs no stack.
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
}
