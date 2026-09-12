using System.Numerics;
using Quark.Graphics;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Materials;
using Quark.Kit.Rendering.PostEffects;
using WebGpuSharp;

namespace AdventureGame.Features.Ambience;

// The air lit by a bolt: on a sphere around it, each pixel integrates the light a point source scatters
// along its view ray, in closed form. The scene depth bounds the integral, so the glow dies into the
// ground instead of cutting across it. Drawn on the back faces with no depth test, so it holds from
// inside the sphere and in front of whatever the sphere encloses. The sphere's own normal gives the
// bolt's position back, so placing the mesh is all it takes.
[Material(MaterialTemplate.Unlit)]
struct LightningHaloMaterial : IMaterial {
    public float Radius;      // must match the sphere mesh
    public Vector3 Color;
    public float Intensity;

    public static string Surface => """
        fn surface(surf: SurfaceInput, m: Material) -> Surface {
            var s = surface_default();

            // World positions are camera-relative: the eye is the origin
            let dist = length(surf.world_position);
            let d = surf.world_position / dist;

            // The sphere's centre, from its normal
            let center = surf.world_position - normalize(surf.normal) * m.radius;

            // Closest approach of the ray to the bolt; h clamped so a ray through it never divides by zero
            let b = dot(d, center);
            let h2 = max(dot(center, center) - b * b, 0.25);
            let h = sqrt(h2);

            // Segment to integrate: sphere entry (the eye when inside) to the nearest of this back face and the scene
            let half = sqrt(max(m.radius * m.radius - h2, 0.0));
            let t0 = max(b - half, 0.0);
            let scene = linear_depth(surf.screen_uv) * dist / surf.view_depth;
            let t1 = max(min(dist, scene), t0);

            // In-scattering of a point light through a homogeneous medium: the integral of 1 / |p(t) - L|^2
            let scatter = (atan((t1 - b) / h) - atan((t0 - b) / h)) / h;

            // Soft rim, so the sphere's boundary never reads as an edge
            let rim = 1.0 - h2 / (m.radius * m.radius);

            s.color = vec4f(m.color * m.intensity * scatter * rim * rim, 1.0);
            return s;
        }
        """;

    public static IReadOnlyList<string> Reads => [KitTargets.Depth];
    public static RenderState State => new() {
        Blend = BlendMode.Additive,
        DepthWrite = false,
        DepthCompare = CompareFunction.Always,
        Cull = CullMode.Front
    };
    public static RenderLayer Layer => RenderLayer.Transparent;
}
