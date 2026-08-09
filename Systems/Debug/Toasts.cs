using System.Numerics;
using ImGuiNET;
using Quark.Ecs;

namespace AdventureGame.Systems;

// Fleeting lines of feedback, stacked above the bottom of the screen. Placeholder for the real game
// UI: sequences already speak through it, so only the drawing has to change later.
sealed class Toasts {
    public const double Life = 3;
    public const double Fade = 0.6;

    readonly List<Toast> lines = [];

    public void Show(string text) => lines.Add(new Toast(text, Life));

    public IReadOnlyList<Toast> Lines => lines;

    public void Age(double seconds) {
        for (var i = lines.Count - 1; i >= 0; i--) {
            var line = lines[i] with { Left = lines[i].Left - seconds };
            if (line.Left <= 0)
                lines.RemoveAt(i);
            else
                lines[i] = line;
        }
    }
}

readonly record struct Toast(string Text, double Left);

sealed class ToastPanel(Toasts toasts) : ISystem {
    const float LineHeight = 26;
    const float BottomMargin = 90;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        toasts.Age(deltaTime);
        if (toasts.Lines.Count == 0)
            return;

        var draw = ImGui.GetForegroundDrawList();
        var screen = ImGui.GetIO().DisplaySize;

        // Newest at the bottom, older ones pushed up
        for (var i = 0; i < toasts.Lines.Count; i++) {
            var line = toasts.Lines[i];
            var width = ImGui.CalcTextSize(line.Text).X;
            var y = screen.Y - BottomMargin - (toasts.Lines.Count - 1 - i) * LineHeight;
            var alpha = (float)Math.Clamp(line.Left / Toasts.Fade, 0, 1);

            draw.AddText(new Vector2((screen.X - width) / 2 + 1, y + 1), Rgba(0, 0, 0, alpha * 0.6f), line.Text);
            draw.AddText(new Vector2((screen.X - width) / 2, y), Rgba(1, 1, 1, alpha), line.Text);
        }
    }

    static uint Rgba(float r, float g, float b, float a) => ImGui.GetColorU32(new Vector4(r, g, b, a));
}
