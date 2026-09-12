using System.Numerics;
using Quark.Graphics;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Materials;

namespace AdventureGame.Features.Ambience;

[Material(MaterialTemplate.Unlit)]
struct LightningMaterial : IMaterial {
    public Vector3 Color;
    public float Fade;
    public float Intensity;

    public static string Surface => """
        fn surface(surf: SurfaceInput, m: Material) -> Surface {
            var s = surface_default();
            s.color = vec4f(surf.color.rgb * m.color * m.intensity, m.fade * surf.color.a);
            return s;
        }
        """;

    public static IReadOnlyList<MaterialVarying> Varyings => [new("color", InterstageParameterType.Float4)];
    public static RenderState State => new() { Blend = BlendMode.Additive, DepthWrite = false };
    public static RenderLayer Layer => RenderLayer.Transparent;
}