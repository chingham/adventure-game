using Quark.Kit;

namespace AdventureGame.App;

/// <summary>
/// One slice of the game, installing itself: its services, its systems, its panels. Nothing outside a
/// feature knows what it registers - <see cref="Features"/> only knows the order they install in.
/// <para>
/// Two passes, because features share data both ways: the camera reads how fast the character runs,
/// the character reads how hard a doorway pulls the camera. Everything is provided before anything is
/// built, so a system can inject any feature's service whatever the list order.
/// </para>
/// </summary>
interface IGameFeature {
    /// <summary>Register services. Runs for every feature before any <see cref="Install"/>.</summary>
    void Provide(Game game) { }

    /// <summary>Register systems, panels and entities, and spawn what the world starts with.</summary>
    void Install(Game game) { }
}

static class FeatureServices {
    // A service another feature provided. Throwing here means it was registered in Install rather than
    // in Provide, so the message names the type rather than the Use*() call the engine would blame.
    public static T Shared<T>(this Game game) where T : class =>
        game.Find<T>() ?? throw new InvalidOperationException(
            $"{typeof(T).Name} has not been provided yet; it belongs in a feature's Provide pass.");
}
