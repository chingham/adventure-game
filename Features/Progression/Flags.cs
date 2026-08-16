namespace AdventureGame.Features.Progression;

// Named booleans that outlive the world: an emptied chest, an unlocked door, a quest step passed.
// Anything a hot reload must not forget lives here rather than in a component, since the level is
// rebuilt from scratch every time its file changes. A save file, later, is this set written down.
sealed class Flags {
    readonly SortedSet<string> raised = [];

    public bool Has(string name) => raised.Contains(name);
    public void Set(string name) => raised.Add(name);
    public void Clear(string name) => raised.Remove(name);

    public IReadOnlyCollection<string> Raised => raised;
}
