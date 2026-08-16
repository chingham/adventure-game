using AdventureGame.Debug;
using AdventureGame.Features.Interaction;
using ImGuiNET;
using Quark.Ecs;

namespace AdventureGame.Features.Progression;

// Progress at a glance, with a switch on every flag: tick one to reach the branch behind it without
// replaying whatever raises it. The list is gathered from the level itself, so it follows a hot reload
// with no bookkeeping to keep in sync.
sealed class ProgressionPanel(Flags flags) : ISystem {
    readonly SortedSet<string> names = [];

    public void Update(World world, EntityCommands commands, float deltaTime) {
        using var panel = DebugUi.Panel("Progression");
        if (!panel.Open)
            return;

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
