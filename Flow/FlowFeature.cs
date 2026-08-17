using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Flow;

// The shell: what screen is up, and who owns the cursor while it is.
sealed class FlowFeature : IGameFeature {
    public void Provide(Game game) {
        game.Provide(new GameFlow(game, game.Input));
    }

    public void Install(Game game) {
        game.AddSystem<FlowSystem>(QuarkPhases.Input, Order.Flow);
        game.AddSystem<CursorToggleSystem>(QuarkPhases.Input, Order.CursorToggle);

        var flow = game.Shared<GameFlow>();
        flow.ReplaceAll(ScreenKind.Game);
        
        //flow.Push(ScreenKind.Inventory);   // dev entry point: boot straight into the panel being worked on
    }
}
