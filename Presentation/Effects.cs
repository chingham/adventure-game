using System.Numerics;
using Quark.Kit.Ui;

namespace AdventureGame.Presentation;

/// <summary>
/// The frame: a bevelled gold wire with a specular line down its middle, a glint in the top-left corner,
/// and patches of light blooming and fading along it, cut out of a Perlin field drifting in z. The halo
/// is drawn in the same pass - it is what the distance field gives past the wire - so there is no blur
/// and nothing to keep in sync.
/// </summary>
struct MagicBorder : IUiEffect {
    public Vector4 Warm;
    public Vector4 Cool;
    public Vector4 Spark;
    public float Width;
    public float Falloff;
    public float Scale;
    public float Speed;
    public float Threshold;
    public float Glow;
    public float SparkGlow;
    public float SparkWire;
    public float CornerGlint;
    public Vector2 PerlinOffset;

    public static string Effect => """
        fn hash3(c: vec3f) -> vec3f {
            let q = vec3f(dot(c, vec3f(127.1, 311.7, 74.7)),
                          dot(c, vec3f(269.5, 183.3, 246.1)),
                          dot(c, vec3f(113.5, 271.9, 124.6)));
            return fract(sin(q) * 43758.5453) * 2.0 - 1.0;
        }

        // Classic gradient noise: dot the corner gradients against the offsets, smoothstep between them.
        fn perlin(v: vec3f) -> f32 {
            let i = floor(v);
            let f = v - i;
            let u = f * f * (3.0 - 2.0 * f);

            var g: array<f32, 8>;
            for (var k = 0; k < 8; k = k + 1) {
                let c = vec3f(f32(k & 1), f32((k >> 1) & 1), f32((k >> 2) & 1));
                g[k] = dot(hash3(i + c), f - c);
            }

            let x = mix(vec4f(g[0], g[2], g[4], g[6]), vec4f(g[1], g[3], g[5], g[7]), u.x);
            let y = mix(x.xz, x.yw, u.y);
            return mix(y.x, y.y, u.z) * 1.6;
        }

        fn effect(s: Surface, p: Params) -> vec4f {
            let half = max(p.width * 0.5, 0.001);

            // Across the wire, -1 at its outer edge to 1 at its inner one. The sqrt profile rounds it, so
            // the thread reads as a wire catching the light rather than a flat band.
            //
            // Never narrower than the grid can resolve, though. The colour is read once, at the pixel
            // centre, so a highlight thinner than a pixel is hit on the rows the wire happens to pass
            // through a centre and missed on the others - which is what makes a thin curve beat light and
            // dark along its length. The profile is widened to what a pixel can average, and the highlight
            // dimmed by exactly what it gained in width: the same light, spread instead of speckled.
            let profile = max(half, 1.3);
            let spread = half / profile;
            let across = clamp(s.d / profile, -1.0, 1.0);
            let bevel = sqrt(max(1.0 - across * across, 0.0));

            // Each colour is worth what its alpha says, resolved once here so nothing downstream can forget
            // it. Authoring a wire at a fifth of its strength has to give a fifth of a wire - and the white
            // highlights below are the wire's own light, so they follow the warm one rather than burning at
            // full over a border that was asked to be faint.
            let warm = p.warm.rgb * p.warm.a;
            let cool = p.cool.rgb * p.cool.a;
            let spark = p.spark.rgb * p.spark.a;

            var color = mix(warm, cool, saturate((s.uv.x + s.uv.y) * 0.5));
            color = mix(color * 0.45, color, bevel);
            color += warm * pow(bevel, 8.0) * 0.5 * spread;

            // Aspect-corrected, so the corner glint stays round on a wide panel.
            let aspect = vec2f(s.size.x / max(s.size.y, 0.001), 1.0);
            let glint = pow(saturate(1.0 - length((s.uv - vec2f(0.07, 0.06)) * aspect) * 1.5), 4.0);
            color += vec3f(1.0, 0.97, 0.88) * glint * 1.5 * p.cornerGlint * p.warm.a;

            // Two octaves of Perlin drifting in z, sampled at the nearest point on the wire rather than at
            // the fragment: the field then lives on the border only, constant along the normal, so what
            // spreads outwards is a lit piece of wire glowing and not noise floating in the void.
            let q = vec3f(contourPoint(s) * p.scale + p.perlinOffset, s.time * p.speed);
            let field = perlin(q) * 0.68 + perlin(q * 2.13 + vec3f(19.0)) * 0.32;

            let wire = stroke(s.d, p.width);
            let bleed = exp(-max(abs(s.d) - half, 0.0) / max(p.falloff, 0.001));

            // Cut the crests off: below the threshold nothing lights up, so the glow stays as patches
            // rather than a wash. Squaring keeps their cores hot and their edges soft, and fading them on
            // the halo's own curve keeps a patch a weight in 0..1 - so the mix below stays a mix and never
            // extrapolates the spark colour past itself.
            let ptch = smoothstep(p.threshold, p.threshold + 0.32, field * 0.5 + 0.5);
            let heat = ptch * ptch * bleed;

            // The wire as it was before the patches: this is what the halo spreads, so their only way into
            // it is sparkGlow.
            // The patch tints towards the spark but is carried by the warm colour, so it is the alphas that
            // say how far it may push the wire. Spark alone never governed it: at low heat this mix is very
            // nearly the warm colour, which is why dimming the spark left the patches burning just as bright.
            // sparkWire is the wire's counterpart to sparkGlow - at 0 the patches leave the thread alone and
            // only the halo breathes, which is what an even, quiet border wants.
            let wireColor = color;
            color += mix(warm, spark, heat) * (ptch * 1.4 + heat * 2.2) * p.sparkWire;

            // Wire and halo in one return: opaque() covers, light() only adds, and both are premultiplied
            // so they compose by addition. The halo swells where a patch sits, and stays a plain gold
            // bleed everywhere else.
            //
            // The halo is settled first, because it is what the wire has to share the range with. Whatever
            // the two of them push past what the target can write comes back as the same white, and the
            // sub-pixel gradient the coverage encoded goes with it - which is why an over-driven wire reads
            // two pixels wide whatever its width, and why its curve steps instead of turning. Giving the
            // wire only the room the halo left keeps the core exactly as hot at full coverage, and lets
            // every pixel in between carry the value it earned.
            let halo = wireColor * bleed * (p.glow + heat * p.sparkGlow);
            let headroom = max(vec3f(1.0) - halo, vec3f(0.0));

            return opaque(min(color, headroom), wire) + light(halo);
        }
        """;
}

/// <summary>
/// The panel body: the scene behind, blurred, keeping its luminance but taking the panel's hue - the
/// colorize blend of the mockup, which a flat fill cannot reproduce.
/// </summary>
struct Colorize : IUiEffect {
    public Vector4 Tint;
    public float Blur;
    public float Strength;

    public static string Effect => """
        fn effect(s: Surface, p: Params) -> vec4f {
            let behind = backdropBlur(s.screen, p.blur);
            let luma = dot(behind, vec3f(0.2126, 0.7152, 0.0722));
            let colorized = p.tint.rgb * (0.22 + luma * 1.7);

            return opaque(mix(behind, colorized, p.strength), fill(s.d));
        }
        """;
}

struct TextGlow : IUiEffect {
    public Vector4 Color;
    public Vector4 Halo;
    public float Radius;

    public static string Effect => """
        fn effect(s: Surface, p: Params) -> vec4f {
            return opaque(p.color.rgb, fill(s.d)) + light(p.halo.rgb * p.halo.a * halo(s, p.radius));
        }
        """;
}