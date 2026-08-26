using AdventureGame.App;
using AdventureGame.Presentation.Highlight;
using Quark.Kit;
using Quark.Kit.Assets;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.PostEffects;
using Quark.Numerics;

namespace AdventureGame.Presentation;

// The type the game is set in, what it says out loud, and the post effects gameplay owns. The sky and
// the rest of the chain are authored in the level file.
sealed class PresentationFeature : IGameFeature {
    public void Provide(IServiceRegistry services) {
        // Driven zone by zone rather than authored: a system needs a handle to write every frame, and
        // the file supplies the values through its aspects instead. Grading lands after the tonemap.
        services.Add(locator => locator.Get<DefaultRenderingModule>().AddPostEffect(
            new DepthFogEffect { Color = Color.FromHex("0E0F15"), Density = 0.02f }, fold: true));
        services.Add(locator => locator.Get<DefaultRenderingModule>().AddPostEffect(
            new GradeEffect { Saturation = 1, Warmth = 0 }, PostEffectSpace.Ldr, fold: true));

        // The passes the effect reads from are installed with it rather than beside it, so the handle
        // cannot exist without what it draws.
        services.Add(locator => {
            var rendering = locator.Get<DefaultRenderingModule>();
            HighlightPasses.Install(rendering);

            return rendering.AddPostEffect(new HighlightEffect {
                CharacterFill = Color.FromHex("#00000088"),
                CharacterStroke = Color.FromHex("#FFFFFF11"),
                HighlightFill = Color.FromHex("#FFFFFF22"),
                HighlightStroke = Color.FromHex("#FFFFFFFF"),
                Thickness = 2f
            }, PostEffectSpace.Ldr, fold: true);
        });

        services.Add(locator => Fonts.Build(locator.Get<AssetLibrary>()));
        services.Add<Toasts>();
    }

    public void Install(Game game) {
        game.AddSystem<ToastPanel>(QuarkPhases.LateUpdate, Order.Panel);
    }
}
