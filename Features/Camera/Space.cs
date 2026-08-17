using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Kit.Rendering.PostEffects;
using System.Numerics;
using Quark.Numerics;

namespace AdventureGame.Features.Camera;

// A named volume that says how a place is framed. Membership is a query on the character's position,
// never a stored state: a hot reload can rebuild the whole level under the player's feet and the
// camera still knows where it is. Interiors use it today; an outdoor camera zone is the same brick
// with other numbers.
struct Space {
    public Vector3d Min;
    public Vector3d Max;

    public double Distance;
    public double FieldOfView;
    public bool CenterPivot;

    // Fog density in here; null keeps whatever the world outside uses.
    public double? Fog;

    public readonly Vector3d Center => (Min + Max) * 0.5;
    public readonly double Volume {
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

// The fog outside every Space: the open-air mood of the level, authored once. A Space overrides the
// density inside it; this is what it goes back to.
struct WorldFog {
    public double Density;
    public Vector4 Color;
}

// Drives the ambience of wherever the character stands: the isometric framing and the fog of the
// space he is in. DoorApproachSystem owns the doorway rig and leaves this one alone.
sealed class SpaceSystem(EffectHandle<DepthFogEffect> fog) : ISystem {
    // Framing outside any space - the wide default the rig was authored with.
    const double OpenDistance = 90;
    const double OpenFieldOfView = 0.4;
    const double FocusHeight = 1.3;

    const double DecayRate = 6;

    // Fog crosses to the destination's setting while the doorway shot still holds the camera tight
    // on the character: almost nothing of it is on screen at that zoom, so a near-instant swap goes
    // unseen - and it has settled long before the shot pulls back out into the room.
    const double FogDecayRate = 4;
    const double FogDoorwayDecayRate = 30;

    // Past this much movement in one step the pivot is not travelling, it jumped: snap rather than
    // sweep the world. Adjacent spaces stay under it and still blend.
    const double JumpDistance = 50;

    // Where the open-world fog stands when the level names none of its own
    readonly double fallbackDensity = fog.Value.Density;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        var weather = Weather(world);

        foreach (var row in world.Query<CameraDirector>()) {
            ref var director = ref row.Component1;

            if (!world.TryGet<FollowRig>(director.FollowRig, out var follow))
                continue;
            if (!world.TryGet<RelativeTransform>(follow.Target, out var target))
                continue;

            var position = target.LocalTransform.Position;
            var space = Find(world, position);
            director.InSpace = space is null ? 0 : 1;

            var distance = space?.Distance ?? OpenDistance;
            var fieldOfView = space?.FieldOfView ?? OpenFieldOfView;
            var pivot = space is { CenterPivot: true } room
                ? room.Center
                : position + new Vector3d(0, 0, FocusHeight);

            ref var iso = ref world.Get<CameraRig>(director.IsometricRig);
            iso.Distance = Quark.Kit.Decay.ExpDecay(iso.Distance, distance, DecayRate, deltaTime);
            iso.FieldOfView = Quark.Kit.Decay.ExpDecay(iso.FieldOfView, fieldOfView, DecayRate, deltaTime);
            iso.Pivot = Vector3d.Distance(iso.Pivot, pivot) > JumpDistance
                ? pivot
                : Quark.Kit.Decay.ExpDecay(iso.Pivot, pivot, DecayRate, deltaTime);

            // A near plane authored for a 90 m shot slices a 20 m one in half, so it follows.
            iso.NearPlane = Math.Clamp(iso.Distance / 12, 0.1, 10);

            // Approach is a frame old here - imperceptible on something this smooth.
            var fogRate = double.Lerp(FogDecayRate, FogDoorwayDecayRate, director.Approach);
            var density = space?.Fog ?? weather?.Density ?? fallbackDensity;
            fog.Value = fog.Value with {
                Color = weather?.Color ?? fog.Value.Color,
                Density = (float)Quark.Kit.Decay.ExpDecay(fog.Value.Density, density, fogRate, deltaTime)
            };
        }
    }

    // The level's own fog, if it authored one
    static WorldFog? Weather(World world) {
        foreach (var row in world.Query<WorldFog>())
            return row.Component1;
        return null;
    }

    // The space holding a point, smallest first so a nook inside a hall wins over the hall.
    public static Space? Find(World world, Vector3d position) {
        Space? best = null;
        foreach (var row in world.Query<Space>()) {
            var space = row.Component1;
            if (!space.Contains(position))
                continue;
            if (best is null || space.Volume < best.Value.Volume)
                best = space;
        }
        return best;
    }
}
