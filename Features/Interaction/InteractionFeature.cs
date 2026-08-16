using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Features.Interaction;

// What the player can touch: volumes that fire on entry, and things worth pressing a button at.
sealed class InteractionFeature : IGameFeature {
    public void Install(Game game) {
        game.AddSystem<InteractionSystem>(QuarkPhases.Gameplay, Order.Interaction);
        game.AddSystem<TriggerSystem>(QuarkPhases.Gameplay, Order.Trigger);
    }
}
