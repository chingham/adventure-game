using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Features.Portals;

// Doorways that move the player from one side of a wall to the other.
sealed class PortalFeature : IGameFeature {
    public void Install(Game game) {
        game.AddSystem<PortalSystem>(QuarkPhases.Gameplay, Order.Portal);
    }
}
