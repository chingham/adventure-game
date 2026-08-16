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
    public void Install(Game game) {
        // One UV mapping for every primitive the kit spawns, so tiling stays consistent across shapes
        var uv = UvMapping.Tiled(GreyboxMaterials.TileMeters);
        game.Primitives.DefaultUv = uv;

        var materials = game.Provide(new GreyboxMaterials(game.Rendering, game.Assets));
        var sun = game.Rendering.CreateCascadeMap(new ShadowSettings());
        var terrain = game.Meshes.From(
            GreyboxMeshes.Terrain(16, 8, 24, 12, 0.5f, uv), MeshCollider.Mesh, materials.Danger);

        var spawn = Vector3d.Zero;
        game.World.Setup(world => spawn = LevelPlayground.Build(world, game.Primitives, materials, sun, terrain));
        game.Provide(new LevelSpawn(spawn));

        game.AddSystem(
            game.ToDispose(new LevelReloadSystem(game.Input, game.Primitives, materials)),
            QuarkPhases.Input, Order.LevelReload);
    }
}
