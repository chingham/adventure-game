using System.Numerics;
using AdventureGame.Features.Camera;
using AdventureGame.Level;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Kit.Rendering.Particles;
using Quark.Kit.Rendering.PostEffects;
using Quark.Numerics;

namespace AdventureGame.Features.Ambience;

// Everything a place does to the air, in one pass: how far you see, what it does to colour, and the
// downpour riding over the camera. All three are judged from the same listener and the same shot, and
// each settles at its own rate.
sealed class AmbienceSystem(Ambience ambience) : ISystem {
    // Rates
    //
    // Fog crosses to the destination's setting while the doorway shot still holds the camera tight on
    // the character: almost nothing of it is on screen at that zoom, so a near-instant swap goes unseen,
    // and it has settled long before the shot pulls back out into the room. Colour gets no such cover,
    // so it follows the place more slowly - a fast swap would read as a flicker.
    const double FogRate = 4;
    const double DoorwayFogRate = 30;
    const double GradeRate = 2.5;
    const double RainRate = 1.5;

    // Rain
    //
    // The emitter has no place of its own: it rides over what the camera watches, high enough for the
    // fall to reach the ground, and pushed along the view so the drops land ahead of the character
    // rather than behind him.
    const double Height = 30;
    const double Lead = 0;
    const float FullRate = 5000;    // particles per second at intensity 1

    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (!CameraListener.TryResolve(world, out var listener, out var shot))
            return;

        // Silence is inheritance: what the volume leaves out is what the open air keeps saying
        var zone = Zones.Resolve<AmbienceZone>(world, listener)?.Aspect;
        var open = Zones.Authored<AmbienceZone>(world);

        ApplyFog(zone?.Fog ?? open?.Fog, shot.Approach, deltaTime);
        ApplyGrade(zone?.Grade ?? open?.Grade, deltaTime);
        ApplyRain(world, zone?.Rain ?? open?.Rain, listener, shot.Yaw, deltaTime);
    }

    void ApplyFog(Fog? target, double approach, float deltaTime) {
        if (target is not { } fog)
            return;

        var rate = double.Lerp(FogRate, DoorwayFogRate, approach);
        var handle = ambience.Fog;
        handle.Value = handle.Value with {
            Color = fog.Color,
            Density = (float)Decay.ExpDecay(handle.Value.Density, fog.Density, rate, deltaTime)
        };
    }

    void ApplyGrade(Grade? target, float deltaTime) {
        if (target is not { } grade)
            return;

        var handle = ambience.Grade;
        handle.Value = new GradeEffect {
            Saturation = (float)Decay.ExpDecay(handle.Value.Saturation, grade.Saturation, GradeRate, deltaTime),
            Warmth = (float)Decay.ExpDecay(handle.Value.Warmth, grade.Warmth, GradeRate, deltaTime)
        };
    }

    void ApplyRain(World world, Rain? target, Vector3d listener, double yaw, float deltaTime) {
        if (target is not { } rain)
            return;

        ambience.Weather = new Rain {
            Intensity = Decay.ExpDecay(ambience.Weather.Intensity, rain.Intensity, RainRate, deltaTime),
            Tilt = Decay.ExpDecay(ambience.Weather.Tilt, rain.Tilt, RainRate, deltaTime)
        };

        if (!world.Has<ParticleEmitter>(ambience.RainEmitter))
            return;

        world.Get<ParticleEmitter>(ambience.RainEmitter).Rate =
            (float)(ambience.Weather.Intensity * FullRate);

        // Placed rather than parented: the emitter follows what the camera watches, not the camera, so
        // pulling the shot back does not drag the weather away with it.
        var forward = new Vector3d(Math.Sin(yaw), Math.Cos(yaw), 0);
        world.Get<RelativeTransform>(ambience.RainEmitter).LocalTransform = new Transform(
            listener + forward * Lead + new Vector3d(0, 0, Height),
            Fall(ambience.Weather.Tilt));
    }

    // Straight down, leaning by the zone's tilt. The half-turn is what points the emitter at the ground;
    // the lean rides on top of it about a world axis, so the wind keeps one direction however the camera
    // turns.
    static Quaternion Fall(double tilt) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)tilt.ToRadians())
        * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 180f.ToRadians());
}
