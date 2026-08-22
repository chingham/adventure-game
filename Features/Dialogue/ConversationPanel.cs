using Quark.Kit.Ui;

namespace AdventureGame.Features.Dialogue;

sealed class ConversationPanel(Speech speech) : IUiRecipe {
    public void Compose(UiComposer ui) {
        if (speech.ActiveConversation is not { } conversation) return;
        
        Console.WriteLine("Draw conversation panel");
    }
}