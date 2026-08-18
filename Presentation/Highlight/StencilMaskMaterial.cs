using Quark.Graphics;
using Quark.Kit.Rendering.Materials;
using WebGpuSharp;

namespace AdventureGame.Presentation.Highlight;

[Material(MaterialTemplate.Unlit)]
struct StencilMaskMaterial : IMaterial {
    public static RenderState State => new() { ColorMask = ColorWriteMask.None };

    public static string Surface => """
        fn surface(surf: SurfaceInput, m: Material) -> Surface {
            return surface_default();
        }
        """;
}