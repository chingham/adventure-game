using AdventureGame.App;
using Quark.Ecs;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Features.Dialogue.Text;

enum TextModifier {
    None,
    Shake,
    Wave,
    Glow
}

readonly record struct TextSegment(string Text, TextModifier Modifier, Color? Tint, float Speed);

record RichText(TextSegment[] Segments, TextBeat[] Beats) {
    const int CharacterPerSecond = 40; // Longer words take longer to read. Maybe use syllables instead of characters
    const float SemanticPause = 0.12f; // Pause after a [,.!?] for a more natural reading speed
    
    public static RichText Parse(string text) {
        //"Hello [glow]World[/glow] [wave]Wave[/wave] [shake]Shake[/shake] [color=#ff0000]Red[/color] Normal"
        throw new NotImplementedException();
    }
}

struct TextBeat {
    public int SegmentIndex;
    public int CharacterIndex;
    public float Duration;
    
    // NOTE: Later, we could add blips with vowel, duration and pitch.
}

struct TextReveal {
    public int Head;
    public bool Complete;
}

sealed class Conversation {
    public string Speaker { get; set; }
    public string[] Pages { get; set; }
    
    public int CurrentPage { get; set; }
    public RichText CurrentText { get; set; }
    public TextReveal CurrentReveal { get; set; }
}

sealed class Speech {
    public Conversation? ActiveConversation { get; set; }
    public bool IsBusy => ActiveConversation is not null;

    public void Begin(string speaker, string[] pages) {
        throw new NotImplementedException();
    }
}

sealed class SpeechSystem(Speech speech, IInput input) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (speech.ActiveConversation is not { } talk) return;
        
        // Advance current reveal (animation)
        var reveal = talk.CurrentReveal;
        AdvanceReveal(ref reveal, talk.CurrentText, deltaTime);
        talk.CurrentReveal = reveal;
        
        // If interact button pressed, advance talk
        if (input.Consume(Controls.Interact)) {
            if (AdvanceSpeech(talk)) {
                // If finished, clear active conversation
                speech.ActiveConversation = null;
            }
        }
    }
    
    static bool AdvanceSpeech(Conversation talk) {
        // If not finished revealing, finish reveal
        if (!talk.CurrentReveal.Complete) {
            talk.CurrentReveal = new TextReveal {
                Head = talk.CurrentText.Beats.Length,
                Complete = true
            };
            return false;
        }
        
        // If finished revealing, advance to next page or finish conversation
        if (talk.CurrentPage + 1 < talk.Pages.Length) {
            talk.CurrentPage++;
            talk.CurrentText = RichText.Parse(talk.Pages[talk.CurrentPage]);
            talk.CurrentReveal = new TextReveal();
            return false;
        }
        
        return true;
    }
    
    bool AdvanceReveal(ref TextReveal reveal, RichText text, float deltaTime) {
        throw new NotImplementedException();
    }
}