using AdventureGame.Debug;
using AdventureGame.Features.Camera;
using AdventureGame.Features.Character;
using AdventureGame.Features.Dialogue;
using AdventureGame.Features.Interaction;
using AdventureGame.Features.Inventory;
using AdventureGame.Features.Menu;
using AdventureGame.Features.Platforms;
using AdventureGame.Features.Portals;
using AdventureGame.Features.Progression;
using AdventureGame.Features.Sequences;
using AdventureGame.Flow;
using AdventureGame.Level;
using AdventureGame.Presentation;
using Quark.Kit;

namespace AdventureGame.App;

/// <summary>
/// Everything the game is made of, in install order. Adding a feature is one line here; nothing else
/// in the app knows it exists.
/// </summary>
static class Features {
    // Order only decides what an Install pass may build on - the level before the character that
    // spawns where it says. Services are all registered first, so they never depend on it. Execution
    // order is a separate matter again, and lives in Order.
    static readonly IGameFeature[] All = [
        new PresentationFeature(),
        new FlowFeature(),
        new ProgressionFeature(),
        new LevelFeature(),
        new CameraFeature(),
        new CharacterFeature(),
        new InteractionFeature(),
        new DialogueFeature(),
        new SequenceFeature(),
        new PlatformFeature(),
        new PortalFeature(),
        new InventoryFeature(),
        new MenuFeature(),
        new DebugFeature()
    ];

    public static void InstallAll(Game game) {
        foreach (var feature in All)
            feature.Provide(game);

        foreach (var feature in All)
            feature.Install(game);
    }
}
