using AdventureGame.App;
using AdventureGame.Presentation.Highlight;
using Quark.Kit;
using Quark.Kit.Rendering.PostEffects;
using Quark.Numerics;

namespace AdventureGame.Presentation;

// The type the game is set in, what it says out loud, and the one post effect gameplay owns. The sky
// and the rest of the chain are authored in the level file.
sealed class PresentationFeature : IGameFeature {
    public void Provide(Game game) {
        // Driven zone by zone rather than authored: a system needs a handle to write every frame, and
        // the file supplies the values through its aspects instead. Grading lands after the tonemap.
        game.Provide(game.Rendering.AddPostEffect(
            new DepthFogEffect { Color = Color.FromHex("0E0F15"), Density = 0.02f }));
        game.Provide(game.Rendering.AddPostEffect(
            new GradeEffect { Saturation = 1, Warmth = 0 }, PostEffectSpace.Ldr));

        game.Provide(Fonts.Build(game));
        game.Provide<Toasts>();

        // Highlight / Shadow effect
        HighlightPasses.Install(game.Rendering);

        var highlightFx = game.Rendering.AddPostEffect(new HighlightEffect {
            CharacterFill = Color.FromHex("#00000088"),
            CharacterStroke = Color.FromHex("#FFFFFF11"),
            HighlightFill = Color.FromHex("#FFFFFF22"),
            HighlightStroke = Color.FromHex("#FFFFFFFF"),
            Thickness = 2f
        }, PostEffectSpace.Ldr);
        game.Provide(highlightFx);
    }

    public void Install(Game game) {
        game.AddSystem<ToastPanel>(QuarkPhases.LateUpdate, Order.Panel);
    }
}
