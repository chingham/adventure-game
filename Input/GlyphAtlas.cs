using System.Xml.Linq;
using Quark.Kit.Assets;
using Quark.Kit.Rendering;
using Quark.Numerics;

namespace AdventureGame.Input;

sealed class GlyphAtlas {
    const string FolderPath = "../../../Data/InputGlyphs";
    
    public GlyphAtlas(string name, AssetLibrary assets) {
        // Load XML descriptor
        var path = Path.GetFullPath(Path.Combine(FolderPath, $"{name}.xml"));
        
        using var stream = File.OpenRead(path);
        var document = XDocument.Load(stream);
        var root = document.Root!;
        
        // Load texture
        var texturePath = Path.GetFullPath(Path.Combine(FolderPath, root.Attribute("imagePath")!.Value));
        
        Texture = assets.LoadTexture(texturePath);
        
        // Read glyphs
        foreach (var glyph in root.Elements("SubTexture")) {
            var glyphName = glyph.Attribute("name")!.Value;
            var x = float.Parse(glyph.Attribute("x")!.Value);
            var y = float.Parse(glyph.Attribute("y")!.Value);
            var width = float.Parse(glyph.Attribute("width")!.Value);
            var height = float.Parse(glyph.Attribute("height")!.Value);
            
            glyphs[glyphName] = new Rect(x, y, x + width, y + height);
        }
    }

    public TextureHandle Texture { get; }

    readonly Dictionary<string, Rect> glyphs = [];
    public bool TryGetGlyphRect(string name, out Rect rect) => glyphs.TryGetValue(name, out rect);
}