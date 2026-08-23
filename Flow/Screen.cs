using Quark.Platform.Input;

namespace AdventureGame.Flow;

/// <summary>
/// A screen is a mode of the game, which may or may not be on top of other screens.<br/>
/// It defines what input groups are active, whether the world is paused, and how fast time runs.
/// </summary>
public sealed record Screen(string Name) {
    public IReadOnlyList<string> InputGroups { get; init; } = [];
    public bool Pause { get; init; } = false;
    public float TimeScale { get; init; } = 1;
    public CursorMode Cursor { get; init; } = CursorMode.Locked;
    public bool Backable { get; init; }
}