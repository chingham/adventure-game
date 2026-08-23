using AdventureGame.App;
using AdventureGame.Features.Dialogue.Text;
using AdventureGame.Features.Sequences;
using Quark.Ecs;
using Quark.Platform.Input;

namespace AdventureGame.Features.Dialogue;

sealed class SpeechSystem(Speech speech, IInput input) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (speech.ActiveConversation is not { } talk) return;
        
        // Advance current reveal (animation)
        var reveal = talk.CurrentReveal;
        AdvanceReveal(ref reveal, talk.CurrentText, deltaTime);
        talk.CurrentReveal = reveal;
        
        // Advance farewell timer
        if (speech.AdvanceFarewell > 0f) {
            speech.AdvanceFarewell = Math.Max(0, speech.AdvanceFarewell - deltaTime);
        }
        
        // If interact button pressed, advance talk
        if (input.Consume(Controls.AdvanceDialogue)) {
            if (AdvanceSpeech(talk)) {
                // If finished, clear active conversation
                var conversationId = speech.End();
                if (conversationId is not null) {
                    world.Events<Signal>().Write(new Signal(conversationId + ".done"));
                }
            }
            else {
                speech.AdvanceFarewell = speech.ActiveConversation.CurrentReveal.Complete ? 0f : 1f;
            }
        }
    }
    
    static bool AdvanceSpeech(Conversation talk) {
        // If not finished revealing, finish reveal
        if (!talk.CurrentReveal.Complete) {
            var text = talk.CurrentText;
            var lastBeat = text.Beats[^1];
            talk.CurrentReveal = new TextReveal {
                BeatIndex = text.Beats.Length - 1,
                CharacterIndex = lastBeat.CharacterCount,
                Clock = lastBeat.Duration + lastBeat.PauseAfter,
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
    
    static bool AdvanceReveal(ref TextReveal reveal, RichText text, float deltaTime) {
        // If already complete, nothing to do
        if (reveal.Complete) return true;
        
        if (reveal.BeatIndex >= text.Beats.Length) {
            reveal.BeatIndex = text.Beats.Length - 1;
            reveal.CharacterIndex = text.Beats[^1].CharacterCount;
            reveal.Clock = text.Beats[^1].Duration;
            reveal.Complete = true;
            return true;
        }

        // Iterate through beats until we reach the end of the time slice
        var remaining = deltaTime;
        while (true) {
            
            // Get the current beat
            var beat = text.Beats[reveal.BeatIndex];
            var beatTotalDuration = beat.Duration + beat.PauseAfter;
            
            // Advance the clock
            var advance = float.Clamp(beatTotalDuration - reveal.Clock, 0, remaining);
            remaining -= advance;
            reveal.Clock += advance;
            
            // If we haven't reached the end of the beat, exit
            if (reveal.Clock < beatTotalDuration) {
                var t = beat.Duration > 0f ? Math.Min(1f, reveal.Clock / beat.Duration) : 1f;
                reveal.CharacterIndex = t * beat.CharacterCount;
                break;
            }
            
            // We have reached the end of the beat
            // If there is no other beat, finish
            if (reveal.BeatIndex == text.Beats.Length - 1) {
                reveal.CharacterIndex = beat.CharacterCount;
                reveal.Clock = beatTotalDuration;
                reveal.Complete = true;
                return true;
            }
            
            // Advance to the next beat
            reveal.BeatIndex++;
            reveal.CharacterIndex = 0;
            reveal.Clock = 0;
            
            // Exit if we have no remaining time to advance
            if (remaining <= 0f) break;
        }
        
        return false;
    }
}