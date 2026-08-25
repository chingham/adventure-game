using System.Numerics;
using Quark.Kit.Rendering.PostEffects;

namespace AdventureGame.Presentation.Highlight;

struct HighlightEffect : IPostEffect {
    public static IReadOnlyList<string> Reads => [KitTargets.Stencil];
    
    public Vector4 CharacterFill;
    public Vector4 CharacterStroke;
    public Vector4 HighlightFill;
    public Vector4 HighlightStroke;
    public float Thickness;
    
    public static string Effect => """
        const CHARACTER: u32 = 2u;
        const HIGHLIGHT: u32 = 4u;
        
        fn post(color: vec3f, uv: vec2f, p: Params) -> vec3f {
            let step = texel_size() * p.thickness;
            
            let inside = sample_stencil(uv);
            let around =
                sample_stencil(uv + vec2f(step.x, 0.0)) |
                sample_stencil(uv - vec2f(step.x, 0.0)) |
                sample_stencil(uv + vec2f(0.0, step.y)) |
                sample_stencil(uv - vec2f(0.0, step.y));
                
            let characterIn = f32((inside & CHARACTER) != 0u);
            let highlightIn = f32((inside & HIGHLIGHT) != 0u);
            
            let character = vec2f(characterIn, saturate(f32((around & CHARACTER) != 0u) - characterIn));
            let highlight = vec2f(highlightIn, saturate(f32((around & HIGHLIGHT) != 0u) - highlightIn));
            
            var c = color;
            c = mix(c, p.characterFill.rgb, character.x * p.characterFill.a);
            c = mix(c, p.characterStroke.rgb, character.y * p.characterStroke.a);
            c = mix(c, p.highlightFill.rgb, highlight.x * p.highlightFill.a);
            c = mix(c, p.highlightStroke.rgb, highlight.y * p.highlightStroke.a);
            return c;
        }
        """;
}