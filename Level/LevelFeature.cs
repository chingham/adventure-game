using System.Numerics;
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
sealed record LevelSpawn(Vector3d Point, double Yaw);

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
        var spawn = SpawnPoint(game, level);
        
        game.Provide(level);
        game.Provide(spawn);
    }

    // The spawn marker is an ordinary entity, so it moves with the level like anything else - but it is
    // read here and never again.
    static LevelSpawn SpawnPoint(Game game, SceneFileHandle level) {
        if (!level.TryEntity("spawn", out var marker) || !game.World.TryGet<RelativeTransform>(marker, out var t))
            return new LevelSpawn(Vector3d.Zero, 0.0);

        var position = t.LocalTransform.Position;

        var orientation = Vector3.Transform(Vector3.UnitY, t.LocalTransform.Rotation);
        var direction = Vector2.Normalize(new Vector2(orientation.X, orientation.Y));
        var yaw = MathF.Atan2(direction.Y, direction.X);

        return new LevelSpawn(position, yaw);
    }
}
