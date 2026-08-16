using AdventureGame.App;
using AdventureGame.Flow;
using AdventureGame.Presentation;
using Quark.Kit;
using Quark.Kit.Ui;

namespace AdventureGame.Features.Inventory;

// What the player carries, and the panel that shows it - icons and live 3D props included.
sealed class InventoryFeature : IGameFeature {
    public void Provide(Game game) {
        game.Provide<Inventory>();
    }

    public void Install(Game game) {
        var icons = Icons.Build(game);
        var showroom = InventoryShowroom.Build(game);

        game.Ui.Add(new InventoryPanel(
            game.Ui, game.Shared<GameFlow>(), game.Shared<Fonts>(), icons, game.Shared<Inventory>(), showroom));
    }
}
