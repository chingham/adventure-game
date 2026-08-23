using AdventureGame.App;
using AdventureGame.Features.Dialogue.Text;
using AdventureGame.Flow;

namespace AdventureGame.Features.Dialogue;

sealed class Speech(GameFlow flow) {
    public Conversation? ActiveConversation { get; private set; }
    public bool IsBusy => ActiveConversation is not null;

    int id;
    IDisposable? screen;

    public string? Begin(string speaker, string[] pages) {
        if (IsBusy) {
            Console.WriteLine($"Speech.Begin: Cannot begin new conversation with {speaker} because another conversation is already active with {ActiveConversation!.Speaker}");
            return null;
        }
        
        var conversationId = $"speech.{id++}";
        
        ActiveConversation = new Conversation {
            Id = conversationId,
            Speaker = speaker,
            Pages = pages,
            CurrentPage = 0,
            CurrentText = RichText.Parse(pages[0]),
            CurrentReveal = new TextReveal()
        };

        screen = flow.Open(Screens.Dialogue);
        
        return conversationId;
    }

    public string? End() {
        if (ActiveConversation is null) {
            Console.WriteLine("Speech.End: Cannot end conversation because no conversation is active");
            return null;
        }
        
        var id = ActiveConversation.Id;
        
        ActiveConversation = null;
        
        screen?.Dispose();
        screen = null;
        
        return id;
    }
}