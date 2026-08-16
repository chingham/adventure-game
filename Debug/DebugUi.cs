using ImGuiNET;

namespace AdventureGame.Debug;

/// <summary>
/// Scoped ImGui windows. <c>Begin</c> has to be matched by <c>End</c> whether it returned true or not,
/// which is the one thing every hand-written panel got wrong sooner or later.
/// </summary>
static class DebugUi {
    public static Window Panel(string title) => new(title);

    public readonly ref struct Window {
        public bool Open { get; }

        internal Window(string title) => Open = ImGui.Begin(title);

        public void Dispose() => ImGui.End();
    }
}
