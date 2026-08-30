using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Rendering.Features;
using Quark.Kit.Rendering.PostEffects;

namespace AdventureGame.Features.Camera;

sealed record Ambience(
    EffectHandle<GradeEffect> Grade,
    SurfaceFeatureHandle<DepthFogFeature> Fog, 
    Entity RainEmitter);

// The post effects a zone drives. They are created in code rather than authored in the file's view
// chain, because a handle is what lets a system write them every frame - the file authors their
// values instead, on the aspect with no volume.

// Fog crosses to the destination's setting while the doorway shot still holds the camera tight on the
// character: almost nothing of it is on screen at that zoom, so a near-instant swap goes unseen - and
// it has settled long before the shot pulls back out into the room.
sealed class FogZoneSystem(Ambience ambience) : ISystem {
    const double DecayRate = 4;
    const double DoorwayDecayRate = 30;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (!Zones.TryListener(world, out var position))
            return;

        var zone = Zones.Resolve<FogZone>(world, position)?.Aspect;
        if (zone is not { } target)
            return;

        // Approach is a frame old here - imperceptible on something this smooth
        var approach = 0.0;
        foreach (var row in world.Query<CameraDirector>())
            approach = row.Component1.Approach;

        var rate = double.Lerp(DecayRate, DoorwayDecayRate, approach);
        var fog = ambience.Fog;
        fog.Value = fog.Value with {
            Color = target.Color,
            Density = (float)Decay.ExpDecay(fog.Value.Density, target.Density, rate, deltaTime)
        };
    }
}

// Colour follows the place at its own pace: slower than the fog, since nothing hides it during a
// crossing and a fast swap would read as a flicker.
sealed class GradeZoneSystem(Ambience ambience) : ISystem {
    const double DecayRate = 2.5;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (!Zones.TryListener(world, out var position))
            return;

        var zone = Zones.Resolve<GradeZone>(world, position)?.Aspect;
        if (zone is not { } target)
            return;

        var grade = ambience.Grade;
        grade.Value = new GradeEffect {
            Saturation = (float)Decay.ExpDecay(grade.Value.Saturation, target.Saturation, DecayRate, deltaTime),
            Warmth = (float)Decay.ExpDecay(grade.Value.Warmth, target.Warmth, DecayRate, deltaTime)
        };
    }
}
