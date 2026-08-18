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
        
        fn fill(uv: vec2f, mask: u32) -> f32 {
            return stencil_coverage(uv, mask);
        }
        fn stroke(uv: vec2f, mask: u32, step: vec2f) -> f32 {
            let inside = stencil_coverage(uv, mask);
            let around = max(
                max(stencil_coverage(uv + vec2f(step.x, 0), mask),
                    stencil_coverage(uv - vec2f(step.x, 0), mask)),
                max(stencil_coverage(uv + vec2f(0, step.y), mask),
                    stencil_coverage(uv - vec2f(0, step.y), mask)));
                    
            return saturate(around - inside);
        }
        
        fn post(color: vec3f, uv: vec2f, p: Params) -> vec3f {
            //let s = sample_stencil(uv);
            //let sc = vec3f(f32(s & 1u), f32((s >> 1u) & 1u), f32((s >> 2u) & 1u));
            //return mix(color, sc, 0.8);
            
            let step = texel_size() * p.thickness;
            
            var c = color;
            c = mix(c, p.characterFill.rgb, fill(uv, CHARACTER) * p.characterFill.a);
            c = mix(c, p.characterStroke.rgb, stroke(uv, CHARACTER, step) * p.characterStroke.a);
            c = mix(c, p.highlightFill.rgb, fill(uv, HIGHLIGHT) * p.highlightFill.a);
            c = mix(c, p.highlightStroke.rgb, stroke(uv, HIGHLIGHT, step) * p.highlightStroke.a);
            return c;
        }
        """;
}