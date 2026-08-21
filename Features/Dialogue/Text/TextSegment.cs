using Quark.Numerics;

namespace AdventureGame.Features.Dialogue.Text;

readonly record struct TextSegment(string Text, TextModifier Modifiers, Color? Tint, float Speed);