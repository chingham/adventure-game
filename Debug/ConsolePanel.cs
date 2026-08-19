using AdventureGame.Features.Sequences;
using ImGuiNET;
using Quark.Ecs;
using Quark.Kit.Components;

namespace AdventureGame.Debug;

sealed class ConsolePanel : ISystem {
    readonly EventReader<Signal> signals = new();
    readonly EventReader<TriggerEvent> triggers = new();
    readonly List<(DateTimeOffset, string)> recentLogs = [];
    
    const int MaxRecentLogs = 20;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        using var panel = DebugUi.Panel("Console");
        if (!panel.Open)
            return;

        foreach (var e in signals.Read(world)) {
            recentLogs.Add((DateTimeOffset.Now, $"[Signal] {e.Name}"));
            if (recentLogs.Count > MaxRecentLogs)
                recentLogs.RemoveAt(0);
        }
        foreach (var e in triggers.Read(world)) {
            recentLogs.Add((DateTimeOffset.Now, $"[Trigger] {e.Kind} '{ZoneName(world, e.Trigger)}"));
            if (recentLogs.Count > MaxRecentLogs)
                recentLogs.RemoveAt(0);
        }
        
        foreach (var (time, line) in recentLogs)
            ImGui.Text($"[{time:HH:mm:ss}] {line}");
    }
    
    static string ZoneName(World world, Entity zone) =>
        world.TryGet<Name>(zone, out var name) ? name.Value : $"#{zone.Index}";
}
