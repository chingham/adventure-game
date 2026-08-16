using System.Numerics;
using Quark.Kit;
using Quark.Kit.Rendering;
using Quark.Kit.Ui;
using Quark.Numerics;

namespace AdventureGame.Features.Inventory;

/// <summary>
/// Four icons and a frame carved out of one texture, drawn into its pixels rather than loaded, so the demo
/// carries no binary asset and still exercises the real path: a <see cref="UiImage.Region"/> apiece, one
/// texture, and therefore a single batch however many of them are up.
/// <para>
/// <see cref="Frame"/> is the nine-slice case, and deliberately a region of the shared atlas rather than a
/// texture of its own - which is what proves <c>s.cell</c> gets the remap to the right tile.
/// </para>
/// </summary>
sealed record Icons(UiImage Potion, UiImage Ring, UiImage Gem, UiImage Key, UiImage Frame) {
    public const float Tile = 32f;

    /// <summary>Corner of <see cref="Frame"/> that stays 1:1 when it is stretched.</summary>
    public const float FrameSlice = 11f;

    const uint Wide = 96;
    const uint High = 64;

    public static Icons Build(Game game) {
        var pixels = new byte[Wide * High * 4];

        Paint(pixels, 0, 0, new Color(1.0f, 0.45f, 0.35f), Disc);
        Paint(pixels, 1, 0, new Color(0.55f, 0.80f, 1.0f), Annulus);
        Paint(pixels, 0, 1, new Color(0.65f, 1.0f, 0.60f), Diamond);
        Paint(pixels, 1, 1, new Color(1.0f, 0.85f, 0.45f), Bar);
        PaintFrame(pixels, 2, 0);

        // Linear rather than sRGB: the UI composites in display space, so the bytes have to reach the shader
        // as authored. No mips either - an atlas blended down a chain reads its neighbours' pixels.
        var texture = game.Rendering.Textures.Create(
            TextureAssetFactory.Rgba32("Demo Icons", Wide, High, pixels) with { GenerateMips = false });

        var atlas = game.Ui.Image(texture);
        return new Icons(
            atlas.Region(0f, 0f, Tile, Tile),
            atlas.Region(Tile, 0f, Tile, Tile),
            atlas.Region(0f, Tile, Tile, Tile),
            atlas.Region(Tile, Tile, Tile, Tile),
            atlas.Region(Tile * 2f, 0f, Tile, Tile));
    }

    public (string Name, UiImage Image)[] All =>
        all ??= [("Potion de soin", Potion), ("Anneau", Ring), ("Gemme solaire", Gem), ("Clé", Key)];

    (string Name, UiImage Image)[]? all;

    // Shapes, as signed distances in tile pixels - negative inside, centred on the tile
    static float Disc(Vector2 p) => p.Length() - 12f;
    static float Annulus(Vector2 p) => MathF.Abs(p.Length() - 11f) - 3f;
    static float Diamond(Vector2 p) => MathF.Abs(p.X) + MathF.Abs(p.Y) - 14f;
    static float Bar(Vector2 p) => MathF.Max(MathF.Abs(p.X) - 12f, MathF.Abs(p.Y) - 4f);

    // One tile, antialiased off its own distance field - the idea the shapes and the glyphs already run on.
    static void Paint(byte[] pixels, uint tileX, uint tileY, Color color, Func<Vector2, float> shape) {
        Write(pixels, tileX, tileY, (local, _) => (color, Coverage(-shape(local))));
    }

    // The nine-slice tile: a dark plate under a bright wire, both on a rounded box whose radius fits inside
    // the slice - a corner wider than the slice would be stretched, and an arc cannot survive that.
    static void PaintFrame(byte[] pixels, uint tileX, uint tileY) {
        var plate = new Color(0.03f, 0.04f, 0.08f);
        var wire = new Color(0.95f, 0.83f, 0.53f);

        Write(pixels, tileX, tileY, (local, _) => {
            var d = RoundedBox(local, new Vector2(15f), 9f);
            var inside = Coverage(-d) * 0.88f;
            var edge = Coverage(1.1f - MathF.Abs(d));

            // Written straight, not premultiplied: the wire has to be its own colour wherever it covers, and
            // the plate shows through only where it does not.
            return (Color.Lerp(plate, wire, edge), MathF.Max(inside, edge));
        });
    }

    static float RoundedBox(Vector2 p, Vector2 half, float radius) {
        var q = Vector2.Abs(p) - (half - new Vector2(radius));
        return Vector2.Max(q, Vector2.Zero).Length() + MathF.Min(MathF.Max(q.X, q.Y), 0f) - radius;
    }

    static float Coverage(float distance) => Math.Clamp(0.5f + distance, 0f, 1f);

    static void Write(byte[] pixels, uint tileX, uint tileY, Func<Vector2, Vector2, (Color, float)> shade) {
        var origin = new Vector2(tileX, tileY) * Tile;

        for (var y = 0u; y < Tile; y++)
        for (var x = 0u; x < Tile; x++) {
            var texel = new Vector2(x, y);
            var (color, alpha) = shade(texel - new Vector2(Tile * 0.5f - 0.5f), texel);
            var offset = (uint)((origin.Y + y) * Wide + origin.X + x) * 4;

            pixels[offset + 0] = Byte(color.R);
            pixels[offset + 1] = Byte(color.G);
            pixels[offset + 2] = Byte(color.B);
            pixels[offset + 3] = Byte(alpha);
        }

        static byte Byte(float value) => (byte)Math.Clamp(value * 255f + 0.5f, 0f, 255f);
    }
}
