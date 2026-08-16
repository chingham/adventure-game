using Quark.Kit;

namespace AdventureGame.App;

/// <summary>
/// One slice of the game, installing itself: its services, its systems, its panels. Nothing outside a
/// feature knows what it registers - <see cref="Features"/> only knows the order they install in.
/// </summary>
interface IGameFeature {
    void Install(Game game);
}

static class FeatureServices {
    // A service another feature provided. Throwing here means the feature order in Features is wrong,
    // so the message names the type rather than the Use*() call the engine would blame.
    public static T Shared<T>(this Game game) where T : class =>
        game.Find<T>() ?? throw new InvalidOperationException(
            $"{typeof(T).Name} has not been provided yet; check the feature order in {nameof(Features)}.");
}
