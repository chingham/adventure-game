using AdventureGame.App;
using Quark.Assets;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Meshes;
using Quark.Kit.Scenes.Files;
using Quark.Numerics;

namespace AdventureGame.Level;

// Where the level file says the player stands. Read once, so a hot reload rebuilds the bricks under
// his feet without teleporting him back to the start.
sealed record LevelSpawn(Vector3d Point);

// The greybox kit and the level authored out of it. The file owns everything it describes: saving it
// re-applies at the next frame boundary, and an invalid save is rejected with the loaded level intact.
sealed class LevelFeature : IGameFeature {
    const string ScenePath = "Data/level.scene.jsonc";
    const string SchemaPath = "Data/level.schema.json";

    // World size the greybox textures span: their inner grid is one metre per cell.
    const float TileMeters = 4f;

    public void Provide(Game game) {
        // One UV mapping for every primitive the kit spawns, so tiling stays consistent across shapes
        game.Primitives.DefaultUv = UvMapping.Tiled(TileMeters);
    }

    public void Install(Game game) {
        // No material: the file gives the terrain entity one by name, like any other brick
        var terrain = game.Meshes.From(
            GreyboxMeshes.Terrain(16, 8, 24, 12, 0.5f, game.Primitives.DefaultUv), MeshCollider.Mesh);

        LevelVocabulary.Register(game.SceneFiles.Vocabulary, terrain);
        game.SceneFiles.WriteSchema(SchemaPath);

        var level = game.SceneFiles.Load(ScenePath);
        game.Provide(level);
        game.Provide(new LevelSpawn(SpawnPoint(game, level)));
    }

    // The spawn marker is an ordinary entity, so it moves with the level like anything else - but it is
    // read here and never again.
    static Vector3d SpawnPoint(Game game, SceneFileHandle level) =>
        level.TryEntity("spawn", out var marker)
        && game.World.TryGet<RelativeTransform>(marker, out var transform)
            ? transform.LocalTransform.Position
            : Vector3d.Zero;
}
