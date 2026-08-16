using AdventureGame.App;
using Quark.Kit;
using Quark.Kit.Components;

namespace AdventureGame.Features.Platforms;

// Platforms that shuttle or come when called. Moves in the fixed step, ahead of whoever stands on it.
sealed class PlatformFeature : IGameFeature {
    public void Install(Game game) {
        game.AddSystem<MovingPlatformSystem>(game.Physics.Phase, Order.MovingPlatform);
        game.AddSystem<PlatformCallSystem>(QuarkPhases.Gameplay, Order.PlatformCall);
    }
}
