using AdventureGame.Features.Interaction;
using ImGuiNET;
using Quark.Ecs;

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

// Progress at a glance, with a switch on every flag: tick one to reach the branch behind it without
// replaying whatever raises it. The list is gathered from the level itself, so it follows a hot reload
// with no bookkeeping to keep in sync.
sealed class FlagsPanel(Flags flags) : ISystem {
    readonly SortedSet<string> names = [];

    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (!ImGui.Begin("Progression")) {
            ImGui.End();
            return;
        }

        Gather(world);

        if (names.Count == 0)
            ImGui.TextDisabled("Nothing to remember yet.");
        else if (ImGui.Button("Clear all"))
            foreach (var name in names)
                flags.Clear(name);

        foreach (var name in names) {
            var raised = flags.Has(name);
            if (!ImGui.Checkbox(name, ref raised))
                continue;

            if (raised)
                flags.Set(name);
            else
                flags.Clear(name);
        }

        ImGui.End();
    }

    // Everything raised, plus every flag the loaded level knows how to raise
    void Gather(World world) {
        names.Clear();

        foreach (var name in flags.Raised)
            names.Add(name);
        foreach (var row in world.Query<Interactable>())
            if (row.Component1.Once)
                names.Add(InteractionSystem.UsedFlag(row.Component1.Emit));
    }
}
