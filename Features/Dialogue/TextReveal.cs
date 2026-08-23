namespace AdventureGame.Features.Dialogue;


/// <summary>
/// The current state of a text reveal,
/// which is the process of showing a text string to the player one character at a time,
/// with optional pauses between words or sentences.
/// </summary>
struct TextReveal {
    
    /// <summary>Whether the text reveal has finished revealing all characters in the current text.</summary>
    public bool Complete;
    
    /// <summary>The index of the current beat in the text reveal.</summary>
    public int BeatIndex;
    
    /// <summary>
    /// The index of the current character in the current beat.<br/>
    /// This is a float because the character index can be a fraction of a character, which is used to animate the reveal of a character over time.
    /// </summary>
    public float CharacterIndex;
    
    internal float Clock;
}