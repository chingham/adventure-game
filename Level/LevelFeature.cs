using AdventureGame.App;
using Quark.Assets;
using Quark.Kit;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Meshes;
using Quark.Numerics;

namespace AdventureGame.Level;

// Where the level file says the player stands. Read once at build, so a hot reload moves the bricks
// without teleporting anybody.
sealed record LevelSpawn(Vector3d Point);

// The greybox kit and the level built out of it, plus the watcher that rebuilds it live.
sealed class LevelFeature : IGameFeature {
    public void Provide(Game game) {
        // One UV mapping for every primitive the kit spawns, so tiling stays consistent across shapes
        game.Primitives.DefaultUv = UvMapping.Tiled(GreyboxMaterials.TileMeters);
        game.Provide(new GreyboxMaterials(game.Rendering, game.Assets));
    }

    public void Install(Game game) {
        var materials = game.Shared<GreyboxMaterials>();
        var sun = game.Rendering.CreateCascadeMap(new ShadowSettings());
        var terrain = game.Meshes.From(
            GreyboxMeshes.Terrain(16, 8, 24, 12, 0.5f, game.Primitives.DefaultUv),
            MeshCollider.Mesh, materials.Danger);

        // Provided from Install rather than Provide: where the player stands is what the level file
        // says, and that is only known once it is built. CharacterFeature installs after this one.
        var spawn = Vector3d.Zero;
        game.World.Setup(world => spawn = LevelPlayground.Build(world, game.Primitives, materials, sun, terrain));
        game.Provide(new LevelSpawn(spawn));

        game.AddSystem(
            game.ToDispose(new LevelReloadSystem(game.Input, game.Primitives, materials)),
            QuarkPhases.Input, Order.LevelReload);
    }
}
