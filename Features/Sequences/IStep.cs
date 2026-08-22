using AdventureGame.Features.Progression;
using Quark.Ecs;

namespace AdventureGame.Features.Sequences;

/// <summary>
/// One beat of a sequence. Each verb is its own type carrying exactly its own fields, so a step that
/// cannot say what it needs does not compile, and the level file gets a schema per verb rather than one
/// object with every key optional.
/// </summary>
interface IStep {
    /// <summary>What the file got wrong about this step, or null. Bound values, not raw JSON.</summary>
    string? Validate() => null;

    /// <summary>Objects this step points at by name, so a load can check they resolve.</summary>
    IEnumerable<string> References() => [];

    /// <summary>Starts the step. Returns null if it is done, or a run to be updated each frame.</summary>
    IStepRun? Start(SequenceRun ctx);
}

/// <summary>
/// A step that is in progress.
/// </summary>
interface IStepRun {
    /// <summary>Updates the step. Returns true if it is done, false if it wants the next frame.</summary>
    bool Update(SequenceRun ctx, float deltaTime);
}