using Quark.Kit.Assets;
using Quark.Kit.Rendering;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Input;

sealed class InputGlyphs {
    public InputGlyphs(AssetLibrary assets) {
        keyboardGlyphs = new GlyphAtlas("pc", assets);
        
        gamepadGlyphs[PadBrand.Xbox] = new GlyphAtlas("xbox", assets);
        gamepadGlyphs[PadBrand.PlayStation] = new GlyphAtlas("playstation", assets);
    }
    
    readonly GlyphAtlas? keyboardGlyphs;
    readonly PadBrand defaultGamepadBrand = PadBrand.Xbox;
    readonly Dictionary<PadBrand, GlyphAtlas> gamepadGlyphs = [];
    
    // Public API
    
    public bool TryGetGlyph(InputKey key, out InputGlyph glyph) {
        if (keyboardGlyphs == null) {
            glyph = default;
            return false;
        }
        
        var assetName = KeyboardAsset(key);
        if (assetName == null) {
            glyph = default;
            return false;
        }
        
        return TryGetGlyph(keyboardGlyphs, assetName, out glyph);
    }
    
    public bool TryGetGlyph(PadBrand brand, PadButton button, out InputGlyph glyph) {
        (brand, var atlas) = NormalizeBrand(brand);

        var assetName = brand switch {
            PadBrand.PlayStation => PlayStationAsset(button),
            _ => XboxAsset(button),
        };
        if (assetName == null) {
            glyph = default;
            return false;
        }
        
        return TryGetGlyph(atlas, assetName, out glyph);
    }
    public bool TryGetGlyph(PadBrand brand, PadTrigger trigger, out InputGlyph glyph) {
        (brand, var atlas) = NormalizeBrand(brand);

        var assetName = brand switch {
            PadBrand.PlayStation => PlayStationAsset(trigger),
            _ => XboxAsset(trigger),
        };
        if (assetName == null) {
            glyph = default;
            return false;
        }
        
        return TryGetGlyph(atlas, assetName, out glyph);
    }
    public bool TryGetGlyph(PadBrand brand, PadStick stick, out InputGlyph glyph) {
        (brand, var atlas) = NormalizeBrand(brand);

        var assetName = brand switch {
            PadBrand.PlayStation => PlayStationAsset(stick),
            _ => XboxAsset(stick),
        };
        if (assetName == null) {
            glyph = default;
            return false;
        }
        
        return TryGetGlyph(atlas, assetName, out glyph);
    }

    // Helpers
    
    (PadBrand, GlyphAtlas) NormalizeBrand(PadBrand brand) {
        if (gamepadGlyphs.TryGetValue(brand, out var atlas)) {
            return (brand, atlas);
        }

        if (gamepadGlyphs.TryGetValue(defaultGamepadBrand, out var defaultAtlas)) {
            return (defaultGamepadBrand, defaultAtlas);
        }

        throw new InvalidOperationException("No gamepad glyph atlas available.");
    }
    
    static bool TryGetGlyph(GlyphAtlas atlas, string name, out InputGlyph glyph) {
        if (atlas.TryGetGlyphRect(name, out var rect)) {
            glyph = new InputGlyph(atlas.Texture, rect);
            return true;
        }
        glyph = default;
        return false;
    }

    // Brand specific mapping
    
    static string? KeyboardAsset(InputKey key) {
        if (key >= InputKey.A && key <= InputKey.Z)
            return $"keyboard_{key.ToString().ToLower()}";
        
        return null;
    }
    
    static string? XboxAsset(PadButton b) {
        return b switch {
            PadButton.South => "xbox_button_a",
            PadButton.East => "xbox_button_b",
            PadButton.West => "xbox_button_x",
            PadButton.North => "xbox_button_y",
            PadButton.LeftBumper => "xbox_lb",
            PadButton.RightBumper => "xbox_rb",
            PadButton.Back => "xbox_button_view",
            PadButton.Start => "xbox_button_menu",
            PadButton.Home => "xbox_guide",
            PadButton.LeftStick => "xbox_stick_l_press",
            PadButton.RightStick => "xbox_stick_r_press",
            PadButton.DPadUp => "xbox_dpad_up",
            PadButton.DPadRight => "xbox_dpad_right",
            PadButton.DPadDown => "xbox_dpad_down",
            PadButton.DPadLeft => "xbox_dpad_left",
            _ => null
        };
    }
    static string? XboxAsset(PadTrigger t) {
        return t switch {
            PadTrigger.Left => "xbox_lt",
            PadTrigger.Right => "xbox_rt",
            _ => null
        };
    }
    static string? XboxAsset(PadStick s) {
        return s switch {
            PadStick.Left => "xbox_stick_l",
            PadStick.Right => "xbox_stick_r",
            _ => null
        };
    }
    
    static string? PlayStationAsset(PadButton b) {
        return b switch {
            PadButton.South => "playstation_button_cross",
            PadButton.East => "playstation_button_circle",
            PadButton.West => "playstation_button_square",
            PadButton.North => "playstation_button_triangle",
            PadButton.LeftBumper => "playstation_trigger_l1",
            PadButton.RightBumper => "playstation_trigger_r1",
            PadButton.Back => "playstation_button_create",
            PadButton.Start => "playstation_button_options",
            PadButton.Home => null,
            PadButton.LeftStick => "playstation_stick_l_press",
            PadButton.RightStick => "playstation_stick_r_press",
            PadButton.DPadUp => "playstation_dpad_up",
            PadButton.DPadRight => "playstation_dpad_right",
            PadButton.DPadDown => "playstation_dpad_down",
            PadButton.DPadLeft => "playstation_dpad_left",
            _ => null
        };
    }
    static string? PlayStationAsset(PadTrigger t) {
        return t switch {
            PadTrigger.Left => "playstation_trigger_l2",
            PadTrigger.Right => "playstation_trigger_r2",
            _ => null
        };
    }
    static string? PlayStationAsset(PadStick s) {
        return s switch {
            PadStick.Left => "playstation_stick_l",
            PadStick.Right => "playstation_stick_r",
            _ => null
        };
    }
}

record struct InputGlyph(TextureHandle Texture, Rect Rect);