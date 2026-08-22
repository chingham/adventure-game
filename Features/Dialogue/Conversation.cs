using AdventureGame.Features.Dialogue.Text;

namespace AdventureGame.Features.Dialogue;

sealed class Conversation {
    public required string Id { get; set; }
    public required string Speaker { get; set; }
    public required string[] Pages { get; set; }
    
    public int CurrentPage { get; set; }
    public required RichText CurrentText { get; set; }
    public TextReveal CurrentReveal { get; set; }
}