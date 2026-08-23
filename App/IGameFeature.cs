using Quark.Kit;

namespace AdventureGame.App;

/// <summary>
/// One slice of the game, installing itself: its services, its systems, its panels. Nothing outside a
/// feature knows what it registers - <see cref="Features"/> only knows the order they install in.
/// <para>
/// Two passes, and each is handed only what it may do. <see cref="Provide"/> gets the registry, which
/// declares without reading: nothing is built while it runs, so no feature can depend on coming after
/// another. <see cref="Install"/> gets the game, by which point every service resolves.
/// </para>
/// </summary>
interface IGameFeature {
    /// <summary>Declare services. Runs for every feature before any of them is built.</summary>
    void Provide(IServiceRegistry services) { }

    /// <summary>Register systems, panels and entities, and spawn what the world starts with.</summary>
    void Install(Game game) { }
}
