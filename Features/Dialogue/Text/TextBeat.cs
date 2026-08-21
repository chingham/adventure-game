namespace AdventureGame.Features.Dialogue.Text;

struct TextBeat {
    public int CharacterIndex;
    public int CharacterCount;
    public float Duration;
    public float PauseAfter;

    // NOTE: Later, we could add blips with vowel, duration and pitch.
}