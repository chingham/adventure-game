using AdventureGame.App;
using Quark.Kit;
using Quark.Kit.Ui;

namespace AdventureGame.Features.Menu;

sealed class MenuFeature : IGameFeature {
    public void Install(Game game) {
        game.AddUi<MenuPanel>();
    }
}
