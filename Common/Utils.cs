using Quark.Numerics;

namespace AdventureGame.Common;

/// <summary>
/// The maths that assume this game's world: Z is up, and yaw is measured the way the character
/// controller measures it. Anything true of any vector lives in <c>Quark.Numerics</c> instead.
/// </summary>
static class Utils {
    /// <summary>Distances below this are noise at this game's scale, in metres.</summary>
    public const double Epsilon = 1e-4;

    // The horizontal plane

    /// <summary>Drops Z, keeping X and Y. The result is NOT normalized.</summary>
    public static Vector3d FlattenXY(Vector3d v) => new(v.X, v.Y, 0);

    /// <summary>The horizontal direction of a vector, or false when it points straight up or down.</summary>
    public static bool TryFlatDir(Vector3d v, out Vector3d dir) {
        dir = FlattenXY(v);
        var len = dir.Length();
        if (len < Epsilon) {
            dir = Vector3d.Zero;
            return false;
        }
        dir /= len;
        return true;
    }

    /// <summary>Projects onto a plane, then flattens: sliding along a wall must not lift or drop us.</summary>
    public static Vector3d ProjectOnPlaneXY(Vector3d v, Vector3d n) {
        var h = FlattenXY(v);
        return TryFlatDir(n, out var u) ? Vector3d.ProjectOnPlane(h, u) : h;
    }

    // Movement

    /// <summary>
    /// Projects onto a plane and rescales to the original length. This is what keeps a walk up a ramp
    /// at walking speed instead of the slower horizontal shadow of it. n must be a unit vector.
    /// </summary>
    public static Vector3d ProjectAndScale(Vector3d v, Vector3d n) {
        var p = Vector3d.ProjectOnPlane(v, n);
        var len = p.Length();
        if (len < Epsilon) return Vector3d.Zero;
        return p * (v.Length() / len);
    }

    /// <summary>Rotates around Z in the character's yaw convention, where yaw = Atan2(x, y).</summary>
    public static Vector3d TurnZ(Vector3d v, double yaw) {
        var (sin, cos) = Math.SinCos(yaw);
        return new Vector3d(v.X * cos + v.Y * sin, v.Y * cos - v.X * sin, v.Z);
    }

    // Cameras

    /// <summary>
    /// Interpolates two fields of view through their focal lengths rather than their angles, so a zoom
    /// reads as even instead of rushing at the wide end.
    /// </summary>
    public static double LerpFov(double a, double b, double t) {
        var aTan = 1 / Math.Tan(a * 0.5);
        var bTan = 1 / Math.Tan(b * 0.5);
        var cTan = double.Lerp(aTan, bTan, t);
        return Math.Atan(1 / cTan) * 2.0;
    }
}
