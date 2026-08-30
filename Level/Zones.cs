using Quark.Ecs;
using Quark.Numerics;

namespace AdventureGame.Level;

/// <summary>
/// A box the level draws around a place. It says nothing on its own: what changes inside it are the
/// aspects riding the same entity - the framing, the ambience. Each resolves on its own, so a nook
/// that only darkens the room keeps everything else the room already decided.
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

readonly record struct Zoned<T>(T Aspect, Volume? Volume) where T : struct;

// Resolution
//
// One component apiece, all optional. A volume carries the aspects it changes and stays silent on the
// rest, and an aspect authored with no volume at all is the level's own - the open air.
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
    /// The aspect the level authored with no volume at all - the open air. Read on its own by whatever
    /// inherits part of an aspect rather than the whole of it.
    /// </summary>
    public static T? Authored<T>(World world) where T : struct {
        foreach (var row in world.Query<T>()) {
            if (!world.Has<Volume>(row.Entity))
                return row.Component1;
        }

        return null;
    }
}
