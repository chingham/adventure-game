using Quark.Numerics;

namespace AdventureGame.Common;

static class Utils {
    public const double Epsilon = 1e-4;
    
    /// <summary>
    /// 2^x.
    /// </summary>
    public static double Exp2(double x) => Math.Pow(2, x);
    
    /// <summary>
    /// Clamps a value between 0 and 1.
    /// </summary>
    public static double Clamp01(double x) => x < 0 ? 0 : x > 1 ? 1 : x;

    /// <summary>
    /// Smoothstep function that interpolates between 0 and 1 with a smooth curve.
    /// </summary>
    public static double SmoothStep01(double x) {
        x = Clamp01(x);
        return x * x * (3 - 2 * x);
    }
    
    /// <summary>
    /// Flattens on the Z plane, keeping only X and Y coordinates.
    /// The result vector is NOT normalized.
    /// </summary>
    public static Vector3d FlattenXY(Vector3d v) => new(v.X, v.Y, 0);

    /// <summary>
    /// Flattens on the Z plane and return true if the vector has any horizontal component.
    /// The result vector is normalized.
    /// </summary>
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
    
    /// <summary>
    /// Projects a vector onto a plane defined by its normal.
    /// n must be a unit-vector.  
    /// </summary>
    public static Vector3d ProjectOnPlane(Vector3d v, Vector3d n) => v - n * Vector3d.Dot(v, n);

    /// <summary>
    /// Projects a vector onto a plane defined by its normal, but only on the XY plane.
    /// </summary>
    public static Vector3d ProjectOnPlaneXY(Vector3d v, Vector3d n) {
        var h = FlattenXY(v);
        return TryFlatDir(n, out var u) ? ProjectOnPlane(h, u) : h;
    }
    
    /// <summary>
    /// Projects a vector onto a plane defined by its normal,
    /// and scales it to keep the same length as the original vector.
    /// n must be a unit-vector.  
    /// </summary>
    public static Vector3d ProjectAndScale(Vector3d v, Vector3d n) {
        var p = ProjectOnPlane(v, n);
        var len = p.Length();
        if (len < Epsilon) return Vector3d.Zero;
        return p * (v.Length() / len);
    }

    /// <summary>
    /// Advances A towards B, limiting the step to maxDelta.
    /// </summary>
    public static Vector3d MoveTowards(Vector3d a, Vector3d b, double maxDelta) {
        var d = b - a;
        var len = d.Length();
        if (len <= maxDelta || len <= Epsilon) return b;
        return a + d / len * maxDelta;
    }

    /// <summary>
    /// Rotates around Z in the character's yaw convention, where yaw = Atan2(x, y).
    /// </summary>
    public static Vector3d TurnZ(Vector3d v, double yaw) {
        var (sin, cos) = Math.SinCos(yaw);
        return new Vector3d(v.X * cos + v.Y * sin, v.Y * cos - v.X * sin, v.Z);
    }

    /// <summary>
    /// Wraps an angle in the range [-PI, PI].
    /// </summary>
    public static double WrapAngle(double a) {
        a = (a + Math.PI) % Math.Tau;
        if (a < 0) a += Math.Tau;
        return a - Math.PI;
    }

    /// <summary>
    /// Clamps a vector's length to a maximum value.
    /// </summary>
    public static Vector3d ClampLength(Vector3d v, double max) {
        var len = v.Length();
        return len <= max || len < Epsilon ? v : v / len * max;
    }
    
    /// <summary>
    /// Linearly interpolates between two angles, taking the shortest path.
    /// </summary>
    public static double LerpAngle(double a, double b, double t) {
        var delta = Utils.WrapAngle(b - a);
        if (delta > Math.PI) {
            delta -= 2.0 * Math.PI;
        }
        return a + delta * t;
    }
    
    /// <summary>
    /// Interpolates between two field-of-view angles, taking into account the non-linear nature of FOV.
    /// </summary>
    public static double LerpFov(double a, double b, double t) {
        var aTan = 1 / Math.Tan(a * 0.5);
        var bTan = 1 / Math.Tan(b * 0.5);
        var cTan = double.Lerp(aTan, bTan, t);
        return Math.Atan(1 / cTan) * 2.0;
    }
}