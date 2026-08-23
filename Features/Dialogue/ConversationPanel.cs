using AdventureGame.Presentation;
using Quark.Kit.Ui;

namespace AdventureGame.Features.Dialogue;

sealed class ConversationPanel(Speech speech, Fonts fonts) : IUiRecipe {
    public void Compose(UiComposer ui) {
        using (ui.Node(Size.Percent(60), Size.Ratio(1 / 8.0f)).Anchor(0.5f, 1f).Margin(20).Enter()) {
            ui.DrawRect(20).Color(0x00000080);
            
            if (speech.ActiveConversation is not { } conversation) return;
            var text = conversation.Pages[conversation.CurrentPage];
            
            using (ui.Node().Expand().Padding(20).Column().Enter()) {
                ui.Text(text).Width(Size.Expand()).Font(fonts.Regular).TextSize(16f).TextColor(0xFFFFFFFF);
            }

            if (conversation.CurrentReveal.Complete) {
                using (ui.Node(Size.Pixels(40), Size.Pixels(40)).Anchor(1, 0.5f).Margin(-20).Enter()) {
                    ui.DrawRect(20).Color(0xFF0000FF);
                }
            }
        }
    }
}