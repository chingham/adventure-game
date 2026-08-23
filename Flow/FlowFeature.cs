using AdventureGame.App;
using Quark.Kit;
using Quark.Platform.Input;

namespace AdventureGame.Flow;

// The shell: what screen is up, and who owns the cursor while it is.
sealed class FlowFeature : IGameFeature {
    public void Provide(IServiceRegistry services) {
        services.Add(locator => new GameFlow(Screens.Game, locator.Get<IInput>(), locator.Get<GameTime>()));
    }

    public void Install(Game game) {
        game.AddSystem<FlowSystem>(QuarkPhases.Input, Order.Flow);
        game.AddSystem<CursorToggleSystem>(QuarkPhases.Input, Order.CursorToggle);
    }
}
