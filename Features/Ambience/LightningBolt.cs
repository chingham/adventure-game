using Quark.Numerics;

namespace AdventureGame.Features.Ambience;

readonly record struct BoltPoint(Vector3d Position, float Intensity);

sealed record BoltPath(BoltPoint[] Points) {
    static readonly Random rand = new();

    const float ForkChance = 0.2f;
    const float ForkFalloff = 0.6f;
    const int MaxForkDepth = 2;
    const float MainFade = 0.2f;
    const float ForkFade = 0.8f;
    static readonly FloatRange ForkBend = new(0.4f, 0.9f);
    static readonly FloatRange ForkReach = new(0.6f, 0.9f);
    
    public static BoltPath[] Generate(Vector3d from, Vector3d to) {
        var branches = new List<BoltPath>();
        Grow(branches, from, to, 6, 1, 0);
        return [.. branches];
    }

    static void Grow(List<BoltPath> into, Vector3d from, Vector3d to, int iterations, float intensity, int depth) {
        var points = new List<Vector3d> { from, to };

        for (var i = 0; i < iterations; i++) {
            var next = new List<Vector3d>(points.Count * 2);

            for (var j = 0; j < points.Count - 1; j++) {
                var a = points[j];
                var b = points[j + 1];
                var mid = Displace(a, b);

                next.Add(a);
                next.Add(mid);

                // Fork
                if (depth < MaxForkDepth && i < iterations - 2 && rand.NextDouble() < ForkChance) {
                    var tip = ForkTip(mid, b);
                    Grow(into, mid, tip, iterations - i - 1, intensity * ForkFalloff, depth + 1);
                }
            }
            
            // Add last point
            next.Add(points[^1]);
            points = next;
        }

        into.Add(Fade(points, intensity, depth));
    }

    static BoltPath Fade(IReadOnlyList<Vector3d> points, float intensity, int depth) {
        var fade = depth == 0 ? MainFade : ForkFade;
        var result = new BoltPoint[points.Count];

        for (var i = 0; i < result.Length; i++) {
            var t = result.Length > 1 ? i / (float)(result.Length - 1) : 0f;
            result[i] = new BoltPoint(points[i], intensity * (1f - t * fade));
        }

        return new BoltPath(result);
    }

    static Vector3d ForkTip(Vector3d a, Vector3d b) {
        var span = b - a;
        var len = span.Length();
        if (len < 1e-6)
            return b;

        var axis = span / len;
        var (right, up) = Basis(axis);
        var angle = rand.NextDouble() * (2.3f - 0.7f) + 0.7f;
        var bend = ForkBend.Lerp(rand.NextSingle());
        var bent = (axis + (right * Math.Cos(angle) + up * Math.Sin(angle)) * bend).Normalized();

        var reach = ForkReach.Lerp(rand.NextSingle());
        return a + bent * (len * reach);
    }
    
    static Vector3d Displace(Vector3d a, Vector3d b) {
        var mid = (a + b) / 2;
        var len = (b - a).Length();

        const float offsetMult = 0.2f;
        
        mid += new Vector3d(
            (rand.NextSingle() * 2 - 1) * len * offsetMult, 
            (rand.NextSingle() * 2 - 1) * len * offsetMult,
            0);
        return mid;
    }

    static (Vector3d Right, Vector3d Up) Basis(Vector3d axis) {
        var reference = Math.Abs(axis.Z) < 0.9 ? Vector3d.UnitZ : Vector3d.UnitX;
        var right = Vector3d.Cross(reference, axis).Normalized();
        var up = Vector3d.Cross(axis, right);
        return (right, up);
    }
}