using AdventureGame.App;
using AdventureGame.Flow;
using AdventureGame.Presentation;
using Quark.Kit;
using Quark.Kit.Ui;

namespace AdventureGame.Features.Inventory;

// What the player carries, and the panel that shows it - icons and live 3D props included.
sealed class InventoryFeature : IGameFeature {
    public void Provide(IServiceRegistry services) {
        services.Add<Inventory>();

        // Both are painted into GPU textures rather than loaded, so they need the live modules - which is
        // what a factory is for. They are built with the rest of the graph, before anything draws.
        services.Add(locator => Icons.Build(locator.Get<Game>()));
        services.Add(locator => InventoryShowroom.Build(locator.Get<Game>()));
    }

    public void Install(Game game) {
        game.AddUi<InventoryPanel>();
    }
}
