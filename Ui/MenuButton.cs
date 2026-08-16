using System.Numerics;
using Quark.Kit.Ui;
using Quark.Numerics;

namespace AdventureGame.Ui;

/// <summary>
/// The menu's own button: a live wire for a border, a lift on highlight, and springs to get there. An
/// instance rather than a function, because it remembers where its animation was - which is also what gives
/// it an identity, so nothing has to be invented to key it with.
/// </summary>
sealed class MenuButton(string label, UiFont font) {
    // Palette
    static readonly Color Background = Color.FromHex("#2266AA00");
    static readonly Color Highlight = Color.FromHex("#2266AAEE");
    static readonly Color Warm = new(0.9f, 0.4f, 0.1f);
    static readonly Color Cool = new(0.8f, 0.5f, 0.2f);
    static readonly Color Spark = new Color(1.0f, 0.8f, 0.5f) * 0.9f;

    // What the wire cools down to once nothing is focused. The alpha dims it, this is what it dims towards.
    static readonly Color Idle = new(0.95f, 0.93f, 0.88f, 0.8f);


    // Shape
    const float Height = 44f;
    const float Radius = 44f;

    // State. Each button rings on its own, from its own place in the noise.
    readonly Vector2 perlinOffset = new(Random.Shared.NextSingle() * 1000f, Random.Shared.NextSingle() * 1000f);
    readonly Spring spring = Spring.FromDuration(0.22f, bounce: 0.35f);
    Motion scaleMotion = new(1f), highlightMotion;

    public bool Compose(UiComposer ui) {
        var node = ui.Node(Size.Expand(), Height)
            .AlignContent(0.5f, 0.5f)
            .Interactive(this)
            .Font(font)
            .TextSize(16f);

        // Read the frame, then let the springs catch up to it
        var highlighted = node.Hovered || node.Focused;
        var pressed = node.Pressed;

        var scale = Settle(ref scaleMotion, pressed ? 0.99f : highlighted ? 1.02f : 1f, spring, ui.DeltaTime);
        var highlight = Settle(ref highlightMotion, pressed ? 0.8f : highlighted ? 1f : 0f, spring, ui.DeltaTime);

        using (node.Enter()) {
            // Background and wire, on a node of their own so the lift never moves the label
            using (ui.Node().Anchor(0f, 0f).Expand().Scale(scale).Enter()) {
                ui.DrawRect(Radius).Color(Color.Lerp(Background, Highlight, highlight));

                ui.DrawRect(Radius).Softness(40f).Snap().Effect(new MagicBorder {
                    Warm = Color.Lerp(Idle, Warm, highlight),
                    Cool = Color.Lerp(Idle, Cool, highlight),
                    Spark = Spark * float.Lerp(0.01f, 0.7f, highlight),
                    Width = float.Lerp(1f, 1.5f, highlight),
                    Falloff = 6f,
                    Scale = 0.008f,
                    Speed = 0.64f,
                    Threshold = float.Lerp(0.1f, 0.25f, highlight),
                    Glow = 0.2f,
                    SparkGlow = float.Lerp(0.002f, 0.7f, highlight),
                    SparkWire = highlight,
                    CornerGlint = 0.4f,
                    PerlinOffset = perlinOffset
                });
            }

            ui.Text(label)
                .TextColor(Color.Lerp(Idle.WithAlpha(1f), Color.White, highlight))
                .TextShadow(new UiTextShadow {
                    Blur = 0f,
                    Color = Color.FromRgba(0x00002280),
                    Offset = new Vector2(0f, float.Lerp(0.25f, 1.0f, highlight))
                });
        }

        return node.OnClick();
    }

    // Follows the target, then parks the value on it once the difference stops showing: the spring only ever
    // approaches asymptotically, and a wire redrawn for a thousandth of a pixel costs what a real move costs.
    // The velocity is left alone, so a bouncy spring crossing its target still rings past it.
    static float Settle(ref Motion motion, float target, Spring spring, float dt) {
        var value = motion.Follow(target, spring, dt);
        return motion.Settled(target, dt) ? target : value;
    }
}
