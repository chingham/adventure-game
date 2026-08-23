using System.Numerics;
using AdventureGame.App;
using AdventureGame.Features.Dialogue.Text;
using AdventureGame.Presentation;
using Quark.Kit.Ui;
using Quark.Numerics;
using Quark.Platform.Input;
using InputGlyphs = AdventureGame.Input.InputGlyphs;

namespace AdventureGame.Features.Dialogue;

sealed class ConversationPanel(Speech speech, Fonts fonts, IInput input, InputGlyphs inputGlyphs) : IUiRecipe {
    readonly Spring spring = Spring.Bouncy;
    Motion advanceButtonMotion;

    UiSpan[] spans = new UiSpan[32];
    int spanCount = 0;
    
    public void Compose(UiComposer ui) {
        if (speech.ActiveConversation is not { } conversation) {
            advanceButtonMotion = default;
            return;
        }
        
        using (ui.Node(Size.Percent(40), Size.Ratio(1 / 5.0f)).Anchor(0.5f, 1f).Margin(20).Enter()) {
            ui.Backdrop();
            //ui.DrawRect(20).Color(0x00000080);
            
            using (ui.Node().Expand().Padding(new Edges(20, 20, 40, 20)).Column().Enter()) {
                // Speaker
                ui.Text(conversation.Speaker)
                    .Width(Size.Expand())
                    .Font(fonts.Regular)
                    .TextSize(23f)
                    .TextColor(Color.Orange);
                
                // Prepare for glyph animation
                var beatStartCharacterIndex = 0;
                for (var i = 0; i < conversation.CurrentReveal.BeatIndex; i++) {
                    var b = conversation.CurrentText.Beats[i];
                    beatStartCharacterIndex += b.CharacterCount;
                }
                
                var index = beatStartCharacterIndex + conversation.CurrentReveal.CharacterIndex;
                
                // Paragraph
                FillSpans(conversation.CurrentText);
                ui.Text(spans.AsSpan()[..spanCount])
                    .Width(Size.Expand())
                    .Font(fonts.Regular)
                    .TextSize(23f)
                    .TextColor(0xFFFFFFFF)
                    .MoveGlyphs((in glyph, ref motion) => {
                        var reveal = 0f;
                        if (glyph.Position < (int)index) {
                            reveal = 1f;
                        } else if (glyph.Position == (int)index) {
                            var t = index - glyph.Position;
                            reveal = t;
                        }

                        //var st = reveal / 0.5f;
                        var translation = 1 - reveal;
                        motion.Opacity = reveal;
                        motion.Offset = new Vector2(0f, translation * 5f);
                    });
            }

            // Continue button
            var reveal = conversation.CurrentReveal.Complete ? 1f : 0f;
            var alpha = advanceButtonMotion.Follow(reveal, spring, ui.DeltaTime);
            
            const float promptKeyBorderRadius = 6f;
            const float targetSize = 30f;
            const float glyphMargin = -2f;
            
            var radius = input.Scheme == InputScheme.KeyboardMouse ? promptKeyBorderRadius : targetSize / 2;
            var hasInteractionImage = inputGlyphs.TryGetImage(Controls.AdvanceDialogue, out var interactionImage);

            var invert = speech.AdvanceFarewell > 0;
            var fillColor = invert ? Color.White : Color.Black;
            var imageColor = invert ? Color.Black : Color.White;

            if (hasInteractionImage) {
                using (ui.Node(Size.Pixels(targetSize), Size.Pixels(targetSize))
                    .Anchor(1, 0.5f)
                    .Margin(-targetSize / 2)
                    .Scale(alpha)
                    .Enter()) {
                    
                    ui.DrawRect(radius).Color(fillColor);

                    var imageNode = ui
                        .Node(Size.Pixels(targetSize - glyphMargin * 2), Size.Pixels(targetSize - glyphMargin * 2))
                        .Margin(glyphMargin);
                    
                    using (imageNode.Enter()) {
                        ui.DrawImage(interactionImage).Color(imageColor);
                    }
                }
            }
        }

        void FillSpans(RichText text) {
            if (text.Segments.Length > spans.Length)
                spans = new UiSpan[text.Segments.Length];
                
            spanCount = 0;
            
            foreach (var segment in conversation.CurrentText.Segments) {
                var span = new UiSpan(segment.Text);

                var textColor = Color.White;
                if (segment.Tint is { } color) {
                    textColor = color;
                    span = span.Color(textColor);
                }
                    
                if (segment.Modifiers.HasFlag(TextModifier.Shake))
                    span = span.MoveGlyphs(UiMotion.Jitter(ui.Time));
                if (segment.Modifiers.HasFlag(TextModifier.Wave))
                    span = span.MoveGlyphs(UiMotion.Wave(ui.Time, 5f, 0.5f));

                if (segment.Modifiers.HasFlag(TextModifier.Glow)) {
                    span = span.Color(Color.White);
                    span = span.Effect(new TextGlow {
                        Color = Color.White,
                        Halo = textColor.WithAlpha(0.25f),
                        Radius = 16
                    });
                }
                
                spans[spanCount++] = span;
            }
        }
    }
}