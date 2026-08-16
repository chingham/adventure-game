using System.Numerics;
using AdventureGame.Inventory;
using Quark.Kit.Ui;
using Quark.Numerics;

namespace AdventureGame.Ui;

/// <summary>
/// The inventory panel of the mockup: the body colorizes the blurred scene rather than covering it, and
/// the frame is a live shader. Only the HDR glow is still missing.
/// </summary>
sealed class InventoryPanel(UiModule ui, GameFlow flow, Fonts fonts, Icons icons, Inventory.Inventory inventory, InventoryShowroom showroom) : IUiRecipe {
    // Palette
    static readonly Color Backdrop = Color.FromRgba(0x00000AC0);
    static readonly Color Body = Color.FromRgba(0x0c0f1ba0);
    static readonly Color Veil = Color.FromRgba(0x2c314530);
    static readonly Color Warm = new(0.9f, 0.4f, 0.1f);
    static readonly Color Cool = new(0.8f, 0.5f, 0.2f);
    static readonly Color Spark = new Color(1.0f, 0.8f, 0.5f) * 0.9f;
    static readonly Color SlotBackground = Color.FromRgba(0x0A0E1860);
    static readonly Color SlotHot = Color.FromRgba(0x2C3145A0);
    static readonly UiTextStyle NameStyle = new() {
        Size = 11f,
        Color = Color.FromRgba(0x9FB2D8FF)
    };

    static readonly UiTextStyle TooltipStyle = new() {
        Size = 13f,
        Color = Color.FromRgba(0xF2D488FF)
    };

    static readonly UiTextStyle TitleStyle = new() {
        Size = 17f, 
        Color = Color.FromRgba(0xF2D488FF),
        Shadow = new UiTextShadow {
            Blur = 0,
            Color = Color.FromRgba(0x00002260), 
            Offset = new Vector2(0, 2)
        }
    };
    static readonly UiTextStyle BodyStyle = new() {
        Size = 14f, 
        Color = Color.FromRgba(0x9FB2D8FF)
    };

    const float TitleFade = 28f;

    readonly UiRamp TitleBackgroundRamp = ui.CreateRamp(Gradient.Ramp(
        (0f, Color.FromRgba(0x00002200)),
        (1f, Color.FromRgba(0x0000221C))));


    readonly UiRamp heroBackgroundRamp = ui.CreateRamp(Gradient.Ramp(
        (0f, Color.FromRgba(0x131923a0)),
        (1f, Color.FromRgba(0x060715a0))));

    // Shape
    const float Width = 960f;
    const float Height = 540f;
    const float Radius = 80f;
    const float TitleLift = 48f;

    // Six cells for four items, so the half-filled row shows the raster holding its shape.
    const int Columns = 7;
    const int Capacity = 35;
    
    // State
    readonly Spring openSpring = Spring.FromDuration(0.27f, bounce: 0.3f);
    readonly Spring closeSpring = Spring.FromDuration(0.2f);
    Motion revealMotion;

    readonly Spring tooltipSpring = Spring.FromDuration(0.26f, bounce: 0.2f);
    MotionRect tooltipMotion;
    
    int selectedSlot = -1;

    public void Compose(UiComposer ui) {
        // Use springs for transition
        var isOpen = flow.IsOpen(ScreenKind.Inventory);
        var spring = isOpen ? openSpring : closeSpring;
        var springTarget = isOpen ? 1 : 0;
        var reveal = revealMotion.Follow(springTarget, spring, ui.DeltaTime);
        if (revealMotion.Settled(0, ui.DeltaTime)) return;
        
        var fade = Math.Clamp(reveal, 0f, 1f);
        var scale = 0.75f + 0.25f * reveal;

        // Main container. Hoverable, so the modal swallows the clicks instead of letting them reach the game.
        using (ui.Node().Expand().AlignContent(0.5f, 0.5f).Opacity(fade).Hoverable().Enter()) {

            // Backdrop (blur + color with alpha)
            ui.DrawRect().Color(Backdrop).BackdropBlur(20f * fade);
            
            // Panel itself
            using (ui.Node(Width, Size.FitContent()).Row().Scale(scale).Enter()) {
                
            
                // Left part (slots)
                using (ui.Node().Padding(new Edges(0, 0, 30, 0)).Margin(new Edges(0, 20, -30, 20)).Enter()) {
                    //ui.DrawRect().Color(Color.Red);
                    PanelFrameLight(ui, 1, 42f);
                    
                    using (ui.Node().Column().Gap(12f).Padding(Edges.Axes(32f, 12f)).Enter()) {
                        using (ui.Node(Size.Expand(), Size.FitContent()).Padding(Edges.Axes(0, 20f)).Enter()) {
                            Slots(ui, 5, 20, 6f / 7f);
                        }
                        
                        using (ui.Node(Size.Expand(), 40).Enter()) {
                            var normalBorderColor = new Color(1f, 0.9f, 0.75f, 0.2f);
                            
                            ui.DrawRect(22f)./*Stroke(Cool.WithAlpha(0.1f), 1f).*/Color(SlotBackground);
                            
                            ui.DrawRect(22f).Softness(40f).Snap().Effect(new MagicBorder {
                                Warm = normalBorderColor,
                                Cool = normalBorderColor,
                                Spark = Spark * 0.5f,
                                Width = 1f,
                                Falloff = 2f,
                                Scale = 0.008f,
                                Speed = 0.64f,
                                Threshold = 0.1f,
                                Glow = 0.1f,
                                SparkGlow = 0.002f,
                                SparkWire = 0.02f,
                                CornerGlint = 0.1f,
                            });
                        }
                    }
                }
            
                // Right part (object details)
                using (ui.Node(Size.Ratio(8f / 7f), Size.Expand()).Enter()) {
                    ui.DrawRect(64f).Effect(new Colorize {
                            Tint = Body,
                            Blur = 40f,
                            Strength = 0.9f
                        })
                        .OuterShadow(new Color(0, 0, 0, 0.27f * reveal), blur: 20f, offset: new Vector2(0, 8 * reveal))
                        .InnerShadow(new Color(0, 0, 0, 0.20f * reveal), blur: 10f, offset: new Vector2(0, 4 * reveal));
                    
                    ui.DrawRect(64f).Texture(heroBackgroundRamp).Effect(new UiGradient {
                        Kind = UiGradientKind.Radial,
                    });
                    
                    ui.DrawRect(64f).Softness(40f).Snap().Effect(new MagicBorder {
                        Warm = Warm,
                        Cool = Cool,
                        Spark = Spark,
                        Width = float.Lerp(8.0f, 1.5f, reveal),
                        Falloff = 6f,
                        Scale = 0.008f,
                        Speed = 0.64f,
                        Threshold = float.Lerp(0.9f, 0.25f, reveal),
                        Glow = float.Lerp(0.9f, 0.2f, reveal),
                        SparkGlow = float.Lerp(1.0f, 0.7f, reveal),
                        SparkWire = 1f,
                        CornerGlint = 0.4f
                    });

                    using (ui.Node().Expand().Row().Margin(new Edges(28f, 56f, 28f, 0)).Enter()) {
                        using (ui.Node(Size.Percent(38), Size.Expand()).Column().Gap(28f).Enter()) {
                            if (selectedSlot >= 0 && selectedSlot < inventory.Items.Length) {
                                var item = inventory.Items[selectedSlot];
                                using (ui.Node(Size.Expand(), Size.FitContent()).Column().AlignContent(0.5f, 0.5f)
                                    .Gap(5f).Enter()) {
                                    ui.Text(item.Name.ToUpperInvariant())
                                        .Width(Size.Expand()).AlignContent(0.5f, 0)
                                        .Font(fonts.Regular).TextSize(32f).TextColor(Color.White);
                                    ui.Text($"- {item.Type} -".ToUpperInvariant())
                                        .Width(Size.Expand()).AlignContent(0.5f, 0)
                                        .Font(fonts.Regular).TextSize(20f).TextColor(Spark);
                                }

                                using (ui.Node(Size.Expand(), Size.FitContent()).Enter()) {
                                    var normalBorderColor = new Color(1f, 0.9f, 0.75f, 0.2f);

                                    ui.DrawRect(22f).Color(SlotBackground);
                                    ui.DrawRect(22f).Softness(40f).Snap().Effect(new MagicBorder {
                                        Warm = normalBorderColor,
                                        Cool = normalBorderColor,
                                        Spark = Spark * 0.5f,
                                        Width = 1f,
                                        Falloff = 2f,
                                        Scale = 0.008f,
                                        Speed = 0.64f,
                                        Threshold = 0.1f,
                                        Glow = 0.1f,
                                        SparkGlow = 0.002f,
                                        SparkWire = 0.02f,
                                        CornerGlint = 0.1f,
                                    });

                                    using (ui.Node().Expand().Column().AlignContent(0.5f, 0.5f).Gap(22f).Padding(18f).Enter()) {
                                        ui.Text(item.Description)
                                            .Width(Size.Percent(100f))
                                            .Font(fonts.Regular).TextSize(18f).TextColor(Color.White.WithAlpha(0.8f));

                                        if (item.Hint != null) {
                                            using (ui.Node(Size.Expand(), 1).Enter()) {
                                                ui.DrawRect().Color(Spark.WithAlpha(0.24f));
                                            }

                                            ui.Text(item.Hint)
                                                .Width(Size.Percent(100f))
                                                .Font(fonts.Regular).TextSize(18f).TextColor(Spark);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            /*using (ui.Node(Width, Height).Column().Scale(scale).Enter()) {
                PanelFrame(ui, reveal, Radius);
                Title(ui, reveal);
                Content(ui);
            }*/
        }
    }

    void PanelFrameLight(UiComposer ui, float reveal, float radius) {
        // Background (colorize + color with alpha)
        ui.DrawRect(radius).Effect(new Colorize {
                Tint = Body,
                Blur = 40f,
                Strength = 0.9f
            })
            .OuterShadow(new Color(0, 0, 0.1f, 0.2f * reveal), blur: 20f, offset: new Vector2(0, 8 * reveal));
        ui.DrawRect(radius).Color(Veil);
                
        // Border
        ui.DrawRect(radius).Softness(40f).Snap().Effect(new MagicBorder {
            Warm = Warm.WithAlpha(0.1f),
            Cool = Cool * 0.9f,
            Spark = Spark * 0.5f,
            Width = float.Lerp(8.0f, 1.0f, reveal),
            Falloff = 2f,
            Scale = 0.008f,
            Speed = 0.64f,
            Threshold = float.Lerp(0.9f, 0.25f, reveal),
            Glow = float.Lerp(0.9f, 0.1f, reveal),
            SparkGlow = float.Lerp(1.0f, 0.2f, reveal),
            SparkWire = 0.2f,
            CornerGlint = 2.7f
        });
    }
    void PanelFrame(UiComposer ui, float reveal, float radius) {
        // Background (colorize + color with alpha)
        ui.DrawRect(radius).Effect(new Colorize {
                Tint = Body,
                Blur = 40f,
                Strength = 0.9f
            })
            .OuterShadow(new Color(0, 0, 0, 0.27f * reveal), blur: 20f, offset: new Vector2(0, 8 * reveal))
            .InnerShadow(new Color(0, 0, 0, 0.20f * reveal), blur: 10f, offset: new Vector2(0, 4 * reveal));
        ui.DrawRect(radius).Color(Veil);
                
        // Border
        ui.DrawRect(radius).Softness(40f).Snap().Effect(new MagicBorder {
            Warm = Warm,
            Cool = Cool,
            Spark = Spark,
            Width = float.Lerp(8.0f, 1.5f, reveal),
            Falloff = 6f,
            Scale = 0.008f,
            Speed = 0.64f,
            Threshold = float.Lerp(0.9f, 0.25f, reveal),
            Glow = float.Lerp(0.9f, 0.2f, reveal),
            SparkGlow = float.Lerp(1.0f, 0.7f, reveal),
            SparkWire = 1f,
            CornerGlint = 0.4f
        });
    }
    void Title(UiComposer ui, float reveal) {
        var titleLift = float.Lerp(TitleLift * 2, TitleLift, reveal);
        using (ui.Node()
            .Anchor(0.5f, 0f)
            .Margin(new Edges(0f, -titleLift, 0f, 0f))
            .AlignContent(0.5f, 0.5f).Padding(new Edges(TitleFade, 0, TitleFade, 0))
            .Font(fonts.Medium)
            .Enter()) {
            ui.DrawRect().Texture(TitleBackgroundRamp).Effect(new UiGradient {
                Kind = UiGradientKind.Edges,
                Extent = TitleFade
            });
            ui.Label("INVENTAIRE", TitleStyle).Tracking(1.2f);
        }
    }
    void Content(UiComposer ui) {
        using (ui.Node().Expand().Font(fonts.Regular).Column().Gap(18f).Padding(34f).Enter()) {
            Slots(ui, Columns, Capacity, slotRatio: 1);
        }
    }
    void Slots(UiComposer ui, int columns, int capacity, float slotRatio) {
        selectedSlot = -1;
        
        using (ui.Node().Expand().Grid(columns).Gap(12f).Enter()) {
            for (var i = 0; i < capacity; i++) {
                Slot(ui, i, slotRatio);
            }
        }
    }

    void Slot(UiComposer ui, int i, float slotRatio) {
        var isEmpty = i >= icons.All.Length;
        var slot = ui.Node(Size.Pixels(64f), Size.Ratio(1 / slotRatio)).AlignContent(0.5f, 0.5f);

        if (!isEmpty) {
            slot.Focusable();
        }
        else {
            slot.Opacity(0.6f);
        }

        if (slot.Focused) {
            selectedSlot = i;
        }
        
        var cornerRadius = 22f;
        var normalBorderColor = new Color(1f, 0.9f, 0.75f, 0.2f);
        var hoveredBorderColor = new Color(1f, 0.9f, 0.75f, 0.4f);
        var focusedBorderColor = new Color(0.2f, 0.7f, 1f, 1f);
        
        var focused = slot.Focused ? 1f : 0f;
        var hovered = slot.Focused ? 0 : slot.Hovered ? 1f : 0f;
        
        using (slot.Enter()) {
            // Background
            ui.DrawRect(cornerRadius).Color(SlotBackground);
            
            // Border
            var borderColor = Color.Lerp(
                Color.Lerp(normalBorderColor, focusedBorderColor, focused),
                hoveredBorderColor,
                hovered);
                    
            var perlinOffset = new Vector2(i * 0.5f, i * 0.5f);
                    
            ui.DrawRect(cornerRadius).Softness(40f).Snap().Effect(new MagicBorder {
                Warm = borderColor,
                Cool = borderColor,
                Spark = Spark * 0.5f,
                Width = 1f,
                Falloff = float.Lerp(2f, 6f, focused),
                Scale = 0.008f,
                Speed = 0.64f,
                Threshold = float.Lerp(0.1f, 0.25f, focused),
                Glow = float.Lerp(0.1f, 0.4f, focused),
                SparkGlow = float.Lerp(0.002f, 0.7f, focused),
                SparkWire = focused,
                CornerGlint = float.Lerp(0.1f, 0.3f, focused),
                PerlinOffset = perlinOffset
            });

            // Image
            if (!isEmpty)
                using (ui.Node().Expand().Column().AlignContent(0.5f, 0.5f).Gap(2f).Enter()) {
                    ui.Image(showroom.Slot(i)).Width(Size.Percent(78f)).Height(Size.Ratio(1));

                    /*ui.Label(icons.All[i].Name, NameStyle)
                        .Width(Size.Percent(100f))
                        .Ellipsis()
                        .AlignContent(0.5f, 0f);*/
                }
        }
    }

    void Tooltip(UiComposer ui, string label, Rect slot) {
        var size = new Vector2(112f, 28f);
        var target = Rect.FromSize(new Vector2(slot.Center.X - size.X * 0.5f, slot.Min.Y - size.Y - 12f), size);

        // On the frame it appears it starts where it is going, instead of flying in from the origin.
        if (tooltipMotion.Value.Width <= 0f)
            tooltipMotion.Value = target;

        var rect = tooltipMotion.Follow(target, tooltipSpring, ui.DeltaTime);

        using (ui.Portal())
        using (ui.Node().At(rect).AlignContent(0.5f, 0.5f).Layer(50).Font(fonts.Regular).Enter()) {
            // A 32 px tile of the icon atlas, stretched to whatever the label needs: the corners keep their
            // arc, the edges only run, and it is still the one batch the icons are in.
            ui.DrawImage(icons.Frame).Effect(UiNineSlice.From(Icons.FrameSlice));
            ui.Label(label, TooltipStyle);
        }
    }
}
