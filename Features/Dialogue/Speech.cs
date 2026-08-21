namespace AdventureGame.Features.Dialogue;

sealed class Speech {
    public Conversation? ActiveConversation { get; set; }
    public bool IsBusy => ActiveConversation is not null;

    public void Begin(string speaker, string[] pages) {
        throw new NotImplementedException();
    }
}