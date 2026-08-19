using AdventureGame.App;
using AdventureGame.Input;
using Quark.Kit;
using Quark.Kit.Ui;

namespace AdventureGame.Features.Interaction;

// What the player can touch: volumes that fire on entry, and things worth pressing a button at.
sealed class InteractionFeature : IGameFeature {
    public void Provide(Game game) {
        game.Provide<InteractionMarkers>();
        game.Provide(new InputGlyphs(game.Assets));
    }

    public void Install(Game game) {
        game.AddSystem<InteractionSystem>(QuarkPhases.Gameplay, Order.Interaction);
        game.AddSystem<TriggerSystem>(QuarkPhases.Gameplay, Order.Trigger);
        
        var markers = game.Shared<InteractionMarkers>();
        var glyphs = game.Shared<InputGlyphs>();
        game.Ui.Add(new InteractionPrompts(markers, glyphs, game.Renderer, game.Input, game.Ui));
    }
}
