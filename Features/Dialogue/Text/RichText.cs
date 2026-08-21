using System.Text;
using Quark.Numerics;

namespace AdventureGame.Features.Dialogue.Text;

// TODO: [pause=0.5] tag
sealed record RichText(TextSegment[] Segments, TextBeat[] Beats) {
    const float CharacterDuration = 1f / 40; // 40 characters per second
    const float SemanticPause = 0.12f; // Pause after a [,.!?] for a more natural reading speed
    
    record struct Tag(string Name, string? Value);
    record struct Segment(string Text, Tag[] Tags);
    
    static int FindEndOfTag(ReadOnlySpan<char> text, int startIndex) {
        for (var i = startIndex; i < text.Length; i++) {
            switch (text[i]) {
                case ']': return i;
                case '[': return -1;
            }
        }
        return -1;
    }
    static TextSegment[] ToTextSegments(Segment[] segments) {
        
        // Convert to TextSegments
        var result = new List<TextSegment>();
        for (var i = 0; i < segments.Length; i++) {
            var segment = segments[i];
            
            var modifiers = TextModifier.None;
            var speed = 1.0f;
            Color? tint = null;
            
            foreach (var tag in segment.Tags) {
                switch (tag.Name.ToLowerInvariant()) {
                    case "shake": modifiers |= TextModifier.Shake; break;
                    case "wave": modifiers |= TextModifier.Wave; break;
                    case "glow": modifiers |= TextModifier.Glow; break;
                    case "color":
                        if (tag.Value is not null && Color.TryFromHex(tag.Value, out var c)) {
                            tint = c;
                        }
                        break;
                    case "speed":
                        if (tag.Value is not null && float.TryParse(tag.Value, out var s)) {
                            speed = s;
                        }
                        break;
                }
            }

            result.Add(new TextSegment(segment.Text, modifiers, tint, speed));
        }
        
        // Merge adjacent segments with the same modifiers and tint
        for (var i = 1; i < result.Count; i++) {
            var prev = result[i - 1];
            var current = result[i];
            if (prev.Modifiers == current.Modifiers &&
                prev.Tint == current.Tint &&
                Math.Abs(prev.Speed - current.Speed) < 0.001f) {
                result[i - 1] = new TextSegment(prev.Text + current.Text, prev.Modifiers, prev.Tint, prev.Speed);
                result.RemoveAt(i);
                i--;
            }
        }
        
        return result.ToArray();
    }
    static TextSegment[] GenerateSegments(ReadOnlySpan<char> text) {
        var currentTags = new List<Tag>();
        var currentText = new StringBuilder();
        var segments = new List<Segment>();

        var index = 0;
        while (index < text.Length) {
            var c = text[index];
            if (c == '[') {
                index++;
                if (index < text.Length && text[index] == '/') {
                    index++;
                    
                    // Closing tag
                    var endIndex = FindEndOfTag(text, index);
                    if (endIndex > index) {

                        // Add current segment
                        if (currentText.Length > 0) {
                            segments.Add(new Segment(currentText.ToString(), currentTags.ToArray()));
                            currentText.Clear();
                        }

                        // Remove the tag from the list
                        var tag = text.Slice(index, endIndex - index);
                        var found = false;
                        for (var j = currentTags.Count - 1; j >= 0; j--) {
                            if (tag.Equals(currentTags[j].Name, StringComparison.OrdinalIgnoreCase)) {
                                currentTags.RemoveAt(j);
                                found = true;
                                break;
                            }
                        }

                        if (found) {
                            // Advance the index past the closing tag
                            index = endIndex + 1;
                            continue;
                        }
                    }
                    
                    index -= 2;
                }
                else {
                    // Start of a tag
                    var endIndex = FindEndOfTag(text, index);
                    if (endIndex > index) {
                        
                        // Add current segment
                        if (currentText.Length > 0) {
                            segments.Add(new Segment(currentText.ToString(), currentTags.ToArray()));
                            currentText.Clear();
                        }

                        // Parse the tag name and value
                        var tag = text.Slice(index, endIndex - index);
                        var equalIndex = tag.IndexOf('=');
                        if (equalIndex >= 0) {
                            // Tag with value
                            var name = tag.Slice(0, equalIndex).ToString();
                            var value = tag.Slice(equalIndex + 1).ToString();
                            currentTags.Add(new Tag(name, value));
                        }
                        else {
                            // Tag without value
                            currentTags.Add(new Tag(tag.ToString(), null));
                        }
                        
                        // Advance the index past the opening tag
                        index = endIndex + 1;
                        continue;
                    }

                    index--;
                }
            }
            
            // Append the character
            currentText.Append(c);
            index++;
        }
        
        if (currentText.Length > 0) {
            segments.Add(new Segment(currentText.ToString(), currentTags.ToArray()));
        }
        
        return ToTextSegments(segments.ToArray());
    }

    static bool IsVowel(char c) => "aeiouAEIOU".IndexOf(c) >= 0;
    static bool IsSemanticBreak(char c) => ",.!?;:".IndexOf(c) >= 0;
    static TextBeat[] GenerateBeats(ReadOnlySpan<char> text) {
        var beats = new List<TextBeat>(text.Length / 2);
        
        var index = 0;
        while (index < text.Length) {
            var start = index;

            if (char.IsLetter(text[index])) {
                // Advance to the last available vowel
                while (index < text.Length && char.IsLetter(text[index]) && !IsVowel(text[index]))
                    index++;
                while (index < text.Length && IsVowel(text[index]))
                    index++;
                
                // Add trailing consonants
                var codaStart = index;
                while (index < text.Length && char.IsLetter(text[index]) && !IsVowel(text[index]))
                    index++;
                
                // But leave the last one for the next vowel (if any)
                if (index > codaStart && index < text.Length && IsVowel(text[index]))
                    index--;
            }
            else if (!IsSemanticBreak(text[index]) && !char.IsWhiteSpace(text[index])) {
                // Non-letter, generate one beat
                // NOTE: Maybe group them (e.g. "129")
                index++;
            }
            
            // Attach trailing punctuation
            var pause = 0f;
            while (index < text.Length && (IsSemanticBreak(text[index]) || char.IsWhiteSpace(text[index]))) {
                if (IsSemanticBreak(text[index]) && (index + 1 >= text.Length || char.IsWhiteSpace(text[index + 1]))) {
                    pause = SemanticPause;
                }
                index++;
            }
            
            // Add the beat if we advanced
            if (index > start) {
                var duration = (index - start) * CharacterDuration ;
                beats.Add(new TextBeat { 
                    CharacterIndex = index, 
                    Duration = duration,
                    PauseAfter = pause
                });
            }
        }
        
        // Assign character counts to each beat
        for (var i = 0; i < beats.Count; i++) {
            var current = beats[i];
            var nextIndex = (i + 1 < beats.Count) ? beats[i + 1].CharacterIndex : text.Length;
            current.CharacterCount = nextIndex - current.CharacterIndex;
            beats[i] = current;
        }
        
        return beats.ToArray();
    }
    
    public static RichText Parse(ReadOnlySpan<char> text) {
        
        // Generate segments
        var segments = GenerateSegments(text);
        
        // Generate beats from the raw text (without tags)
        var cleanedText = new StringBuilder();
        foreach (var segment in segments) {
            cleanedText.Append(segment.Text);
        }
        
        var beats = GenerateBeats(cleanedText.ToString().AsSpan());

        return new RichText(segments, beats);
    }
}
