using AdventureGame.Features.Dialogue.Text;

namespace AdventureGame.Features.Dialogue;

sealed class Speech {
    public Conversation? ActiveConversation { get; private set; }
    public bool IsBusy => ActiveConversation is not null;

    int id;

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
        
        return conversationId;
    }

    public string? End() {
        if (ActiveConversation is null) {
            Console.WriteLine("Speech.End: Cannot end conversation because no conversation is active");
            return null;
        }
        
        var id = ActiveConversation.Id;
        
        ActiveConversation = null;
        
        return id;
    }
}