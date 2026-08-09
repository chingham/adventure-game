using System.Numerics;
using AdventureGame.Library;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Meshes;
using Quark.Numerics;

namespace AdventureGame;

// Data-driven playground: the bricks come from Data/level.json, watched and reloaded live by
// LevelReloadSystem. What carries behaviour rather than shape - sun, terrain, moving platforms - stays
// in code. Same signature as the other playgrounds, so Program swaps between them by class name.
static class LevelPlayground {
    public static void Build(EntityCommands world, GamePrimitiveLibrary p, GreyboxMaterials g, CascadeShadowMap sun, GameMesh terrain) {
        Sun(world, sun);

        if (LevelFile.TryRead(LevelFile.Path, out var bricks) == LevelReadResult.Loaded)
            LevelFile.Apply(bricks, new Bricks(world, p, g), g);

        world.Spawn(terrain).At(new Vector3d(0, -14, 0)).Static(layer: Layers.Environment);
    }

    // Lighting

    static void Sun(EntityCommands world, CascadeShadowMap sun) {
        var lightDirection = Vector3.Normalize(new Vector3(-0.4f, -0.35f, -0.85f));
        world
            .Spawn(Light.Directional(lightDirection, Vector3.One, 1.5f) with { Shadows = sun })
            .At(Vector3d.Zero);
    }

}
