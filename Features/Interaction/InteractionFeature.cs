using AdventureGame.App;
using Quark.Kit;
using Quark.Kit.Assets;
using Quark.Kit.Ui;
using Quark.Platform.Input;
using InputGlyphs = AdventureGame.Input.InputGlyphs;

namespace AdventureGame.Features.Interaction;

// What the player can touch: volumes that fire on entry, and things worth pressing a button at.
sealed class InteractionFeature : IGameFeature {
    public void Provide(IServiceRegistry services) {
        services.Add<InteractionMarkers>();
        services.Add(locator => new InputGlyphs(
            locator.Get<AssetLibrary>(),
            locator.Get<IInput>(),
            locator.Get<UiModule>()
        ));
    }

    public void Install(Game game) {
        game.AddSystem<InteractionSystem>(QuarkPhases.Gameplay, Order.Interaction);
        game.AddSystem<TriggerSystem>(QuarkPhases.Gameplay, Order.Trigger);

        game.AddUi<InteractionPrompts>();
    }
}
