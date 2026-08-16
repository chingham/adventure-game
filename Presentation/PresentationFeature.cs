using AdventureGame.App;
using Quark.Kit;
using Quark.Kit.Rendering.Environments;
using Quark.Kit.Rendering.PostEffects;
using Quark.Numerics;

namespace AdventureGame.Presentation;

// How the game looks before anything is in it: sky, post chain, type. The fog handle is provided
// rather than kept, since SpaceSystem is what drives it from one room to the next.
sealed class PresentationFeature : IGameFeature {
    public void Provide(Game game) {
        Environment(game);
        PostProcess(game);

        game.Provide(Fonts.Build(game));
        game.Provide<Toasts>();
    }

    public void Install(Game game) {
        game.AddSystem<ToastPanel>(QuarkPhases.LateUpdate, Order.Panel);
    }

    // Sky

    static void Environment(Game game) {
        var environment = game.Rendering.CreateEnvironment(new GradientSky {
            Horizon = Color.FromHex("6E6493"),
            Zenith = Color.FromHex("1B2440"),
            Ground = Color.FromHex("0E0F15")
        });
        game.Rendering.SetEnvironment(environment.Value);
    }

    // Post chain

    static void PostProcess(Game game) {
        game.Rendering.AddEffect(new Bloom { Threshold = 1.2f, Radius = 1.3f, Intensity = 0.22f });

        game.Provide(game.Rendering.AddPostEffect(
            new DepthFogEffect { Color = Color.FromHex("0E0F15"), Density = 0.02f }));

        game.Rendering.AddPostEffect(new FilmEffect { Vignette = 0.3f });
        game.Rendering.AddPostEffect(new GradeEffect { Saturation = 0.9f, Warmth = 0.1f });
    }
}
