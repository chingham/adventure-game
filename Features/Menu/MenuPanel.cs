using System.Numerics;
using AdventureGame.Flow;
using AdventureGame.Presentation;
using Quark.Kit.Ui;
using Quark.Numerics;

namespace AdventureGame.Features.Menu;

/// <summary>
/// The inventory panel of the mockup: the body colorizes the blurred scene rather than covering it, and
/// the frame is a live shader. Only the HDR glow is still missing.
/// </summary>
sealed class MenuPanel(UiModule ui, GameFlow flow, Fonts fonts) : IUiRecipe {
    // Palette
    static readonly Color Backdrop = Color.FromRgba(0x00000AC0);


    // Shape
    const float Width = 400f;
    const float TransitionMargin = 200f;
    
    // State
    readonly Spring openSpring = Spring.FromDuration(0.2f, bounce: 0.2f);
    readonly Spring closeSpring = Spring.FromDuration(0.2f);
    Motion revealMotion;
    
    // Buttons
    readonly MenuButton resumeButton = new("REPRENDRE", fonts.Wide);
    readonly MenuButton optionsButton = new("OPTIONS", fonts.Wide);
    readonly MenuButton quitButton = new("QUITTER", fonts.Wide);

    public void Compose(UiComposer ui) {
        // Use springs for transition
        var isOpen = flow.IsOpen(ScreenKind.Menu);
        var spring = isOpen ? openSpring : closeSpring;
        var springTarget = isOpen ? 1 : 0;
        var reveal = revealMotion.Follow(springTarget, spring, ui.DeltaTime);
        if (revealMotion.Settled(0, ui.DeltaTime)) return;
        
        var fade = Math.Clamp(reveal, 0f, 1f);

        // Main container
        var containerWidth = Width + TransitionMargin;
        using (ui.Node(containerWidth, Size.Expand())
            .Translate(-TransitionMargin - containerWidth * (1 - reveal), 0)
            .AlignContent(0, 0.5f)
            .Padding(new Edges(TransitionMargin, 0, 0, 0))
            .Hoverable()
            .Enter()) {
            
            // Backdrop (blur + color with alpha)
            ui.DrawRect()
                .Color(Backdrop)
                .BackdropBlur(20f * fade);

            // Panel container, carrying the type the whole menu is set in
            using (ui.Node(Width, Size.FitContent()).Column().Gap(12).Padding(64).AlignContent(0.5f, 0.5f).Enter()) {

                // Title
                ui.Text(
                        "LittleBig\nAdventure ",
                        new UiSpan("2").Size(72f).Color(Color.Orange)
                    )
                    .Font(fonts.Regular)
                    .TextColor(0xFFEEBBFF)
                    .TextSize(60f)
                    .TextShadow(new UiTextShadow {
                        Blur = 10,
                        Color = Color.FromRgba(0x00000080),
                        Offset = new Vector2(0, 3),
                    })
                    .LineHeight(0.58f)
                    .AlignContent(0.5f, 0);

                ui.Text("Twinsen's Odyssey")
                    .Font(fonts.Regular)
                    .TextColor(0xFFEEBBFF)
                    .TextSize(20f)
                    .TextShadow(new UiTextShadow {
                        Blur = 10,
                        Color = Color.FromRgba(0x00000080),
                        Offset = new Vector2(0, 3),
                    });

                ui.Node(Size.Pixels(0), Size.Pixels(60));
                
                // Buttons
                
                if (resumeButton.Compose(ui)) {
                    flow.TryBack();
                }

                if (optionsButton.Compose(ui)) {
                    Console.WriteLine("Options");
                }

                if (quitButton.Compose(ui)) {
                    Environment.Exit(0);
                }
            }
        }
    }
}
