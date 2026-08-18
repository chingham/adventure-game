using AdventureGame.App;
using AdventureGame.Features.Progression;
using Quark.Kit;
using Quark.Kit.Ui;

namespace AdventureGame.Features.Interaction;

// What the player can touch: volumes that fire on entry, and things worth pressing a button at.
sealed class InteractionFeature : IGameFeature {
    public void Install(Game game) {
        var markers = game.Provide<InteractionMarkers>();
        
        game.AddSystem(new InteractionSystem(game.Input, game.Find<Flags>()!, markers), QuarkPhases.Gameplay, Order.Interaction);
        game.AddSystem<TriggerSystem>(QuarkPhases.Gameplay, Order.Trigger);
        
        game.Ui.Add(new InteractionPrompts(markers, game.Renderer, game.Input));
    }
}
