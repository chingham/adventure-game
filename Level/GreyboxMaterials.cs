using Quark.Kit.Assets;
using Quark.Kit.Rendering;
using Quark.Numerics;

namespace AdventureGame.Level;

sealed class GreyboxMaterials(DefaultRenderingModule rendering, AssetLibrary assets) {
    // World size the greybox textures span: their inner grid is one meter per cell.
    public const float TileMeters = 4f;

    
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
