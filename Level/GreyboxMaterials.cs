using Quark.Kit.Assets;
using Quark.Kit.Rendering;
using Quark.Numerics;

namespace AdventureGame.Level;

sealed class GreyboxMaterials(DefaultRenderingModule rendering, AssetLibrary assets) {
    // World size the greybox textures span: their inner grid is one meter per cell.
    public const float TileMeters = 4f;


    //public readonly MaterialHandle Floor = Matte(rendering, Color.Rgb(0.34f, 0.35f, 0.38f), 0.95f);
    //public readonly MaterialHandle Structure = Matte(rendering, Color.Rgb(0.55f, 0.56f, 0.60f), 0.9f);
    //public readonly MaterialHandle Ramp = Matte(rendering, Color.Rgb(0.80f, 0.45f, 0.28f), 0.8f);
    //public readonly MaterialHandle Stairs = Matte(rendering, Color.Rgb(0.30f, 0.58f, 0.62f), 0.8f);
    //public readonly MaterialHandle Platform = Matte(rendering, Color.Rgb(0.72f, 0.73f, 0.76f), 0.85f);
    //public readonly MaterialHandle MovingPlatform = Matte(rendering, Color.Rgb(0.35f, 0.45f, 0.78f), 0.75f);
    //public readonly MaterialHandle Obstacle = Matte(rendering, Color.Rgb(0.62f, 0.52f, 0.66f), 0.85f);
    //public readonly MaterialHandle Terrain = Matte(rendering, Color.Rgb(0.36f, 0.46f, 0.34f), 0.9f);
    //public readonly MaterialHandle Character = Matte(rendering, Color.Rgb(0.90f, 0.78f, 0.32f), 0.55f);
    
    public readonly MaterialHandle Environment = Textured(rendering, assets, "darkgray", 0.9f);
    public readonly MaterialHandle Neutral = Textured(rendering, assets, "white", 0.8f);
    public readonly MaterialHandle Danger = Textured(rendering, assets, "red", 0.8f);
    public readonly MaterialHandle Interactive = Textured(rendering, assets, "yellow", 0.7f);

    // Authoring name to material, for the data-driven level. Unknown names fall back to the grey.
    public MaterialHandle ByName(string? name) {
        switch (name?.ToLowerInvariant()) {
            case null or "" or "environment": return Environment;
            case "neutral": return Neutral;
            case "danger": return Danger;
            case "interactive": return Interactive;
            default:
                Console.Error.WriteLine($"[Level] Unknown material '{name}', using the environment grey.");
                return Environment;
        }
    }

    static MaterialHandle Matte(DefaultRenderingModule rendering, Color color, float roughness) {
        return rendering.CreateMaterial(new PbrMaterial {
            BaseColor = color,
            Roughness = roughness
        });
    }
    static MaterialHandle Textured(DefaultRenderingModule rendering, AssetLibrary assets, string filename, float roughness) {
        var texture = assets.LoadTexture($"Data/Textures/greybox-unit-{filename}.png");
        return rendering.CreateMaterial(new PbrMaterial {
            BaseColorTexture = texture,
            Roughness = roughness
        });
    }
}
