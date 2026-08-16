using AdventureGame.App;
using AdventureGame.Flow;
using AdventureGame.Presentation;
using Quark.Kit;
using Quark.Kit.Ui;

namespace AdventureGame.Features.Menu;

sealed class MenuFeature : IGameFeature {
    public void Install(Game game) {
        game.Ui.Add(new MenuPanel(game.Shared<GameFlow>(), game.Shared<Fonts>()));
    }
}
