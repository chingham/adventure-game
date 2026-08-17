using System.Numerics;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Features.Camera;

/// <summary>
/// A box the level draws around a place. It says nothing on its own: what changes inside it are the
/// aspects riding the same entity - the framing, the fog, the grading, the weather. Each resolves on
/// its own, so a nook that only darkens the room keeps everything else the room already decided.
/// </summary>
struct Volume {
    public Vector3d Min;
    public Vector3d Max;

    public readonly Vector3d Center => (Min + Max) * 0.5;

    public readonly double Size {
        get {
            var size = Max - Min;
            return size.X * size.Y * size.Z;
        }
    }

    public readonly bool Contains(Vector3d p) =>
        p.X >= Min.X && p.X <= Max.X &&
        p.Y >= Min.Y && p.Y <= Max.Y &&
        p.Z >= Min.Z && p.Z <= Max.Z;
}

// Aspects
//
// One component apiece, all optional. A volume carries the ones it changes and stays silent on the
// rest, and an aspect authored with no volume at all is the level's own - the open air.

// How a place is framed, and whether that framing is the interior one
struct CameraZone {
    public bool Iso;
    public double Distance;
    public double FieldOfView;
    public bool CenterPivot;
}

// How far you see in here
struct FogZone {
    public double Density;
    public Vector4 Color;
}

// What the place does to colour
struct GradeZone {
    public double Saturation;
    public double Warmth;
}

// How hard it rains here. One number, because the emitters, the loop's gain and the haze all read it:
// three settings could drift apart mid-transition, one cannot.
struct RainZone {
    public double Intensity;
    public double Tilt;        // degrees the fall leans, matched by the streaks
}

// Authored, not yet drawn: everything inside goes black, softened over Edge metres. Unlike every other
// aspect this one is not resolved by where the player stands - it is a thing in the world, and
// whatever draws it will have to find it from wherever the camera is.
struct Shroud {
    public double Edge;
    public Vector4 Color;
}

// Resolution

readonly record struct Zoned<T>(T Aspect, Volume? Volume) where T : struct;

static class Zones {
    /// <summary>
    /// The aspect in force at a point: the smallest volume holding it that carries one, else the level's
    /// own. Silence is inheritance - a volume that does not mention an aspect leaves it to the volume
    /// around it.
    /// </summary>
    public static Zoned<T>? Resolve<T>(World world, Vector3d point) where T : struct {
        Zoned<T>? inside = null;
        Zoned<T>? authored = null;
        var smallest = double.MaxValue;

        foreach (var row in world.Query<T>()) {
            if (!world.TryGet<Volume>(row.Entity, out var volume)) {
                authored = new Zoned<T>(row.Component1, null);
                continue;
            }

            if (!volume.Contains(point) || volume.Size >= smallest)
                continue;

            smallest = volume.Size;
            inside = new Zoned<T>(row.Component1, volume);
        }

        return inside ?? authored;
    }

    /// <summary>
    /// Where the ambience is judged from: whoever the camera follows. Read through the rig rather than
    /// from the character directly, so what the player sees and what he hears agree on one position.
    /// </summary>
    public static bool TryListener(World world, out Vector3d point) {
        foreach (var row in world.Query<CameraDirector>()) {
            if (!world.TryGet<FollowRig>(row.Component1.FollowRig, out var follow))
                continue;
            if (!world.TryGet<RelativeTransform>(follow.Target, out var target))
                continue;

            point = target.LocalTransform.Position;
            return true;
        }

        point = default;
        return false;
    }
}
