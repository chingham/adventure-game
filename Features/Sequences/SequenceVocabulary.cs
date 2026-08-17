using System.Text.Json;
using Quark.Kit.Scenes.Files;
using Quark.Scenes;

namespace AdventureGame.Features.Sequences;

// What a level file can say about rules. Registered in the Provide pass, before any file is read.
static class SequenceVocabulary {
    public static void Register(SceneVocabulary vocabulary) {
        vocabulary
            .Verb("sequence", SequenceVerb, order: 300, payloadType: typeof(SequencePayload))

            // Step verbs. A single-field step also takes its value bare: { "emit": "tower-call" }.
            .Variant<IStep, Wait>("wait")
            .Variant<IStep, Emit>("emit")
            .Variant<IStep, WaitSignal>("waitSignal")
            .Variant<IStep, Gate>("gate")
            .Variant<IStep, Release>("release")
            .Variant<IStep, Face>("face")
            .Variant<IStep, SetFlag>("set")
            .Variant<IStep, ClearFlag>("clear")
            .Variant<IStep, Enable>("enable")
            .Variant<IStep, Disable>("disable")
            .Variant<IStep, Move>("move")
            .Variant<IStep, Toast>("toast")
            .Variant<IStep, If>("if");
    }

    sealed class SequencePayload {
        public string? On { get; set; }
        public IStep[] Steps { get; set; } = [];
    }

    // A rule has nowhere to stand in the world; it lives as an entity so the file owns it like the rest.
    static void SequenceVerb(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<SequencePayload>(json, ctx.Location);

        if (string.IsNullOrEmpty(p.On))
            throw ctx.Error(ctx.Location, "A sequence needs 'on'.");
        if (p.Steps.Length == 0)
            throw ctx.Error(ctx.Location, "A sequence needs 'steps'.");

        foreach (var step in p.Steps)
            if (step.Validate() is { } error)
                throw ctx.Error(ctx.Location, error);

        entity.Add(new SequenceDefinition { On = p.On, Steps = p.Steps });
    }
}
