using AdventureGame.App;
using Quark.Kit;
using Quark.Kit.Rendering.PostEffects;
using Quark.Numerics;

namespace AdventureGame.Presentation;

// The type the game is set in, what it says out loud, and the one post effect gameplay owns. The sky
// and the rest of the chain are authored in the level file.
sealed class PresentationFeature : IGameFeature {
    public void Provide(Game game) {
        // Driven room by room by SpaceSystem, so it is provided rather than authored
        game.Provide(game.Rendering.AddPostEffect(
            new DepthFogEffect { Color = Color.FromHex("0E0F15"), Density = 0.02f }));

        game.Provide(Fonts.Build(game));
        game.Provide<Toasts>();
    }

    public void Install(Game game) {
        game.AddSystem<ToastPanel>(QuarkPhases.LateUpdate, Order.Panel);
    }
}
