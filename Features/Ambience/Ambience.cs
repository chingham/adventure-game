using Quark.Ecs;
using Quark.Kit.Rendering.Features;
using Quark.Kit.Rendering.PostEffects;

namespace AdventureGame.Features.Ambience;

/// <summary>
/// What the ambience is written to every frame. The effects are built in code rather than authored in
/// the file's view chain, because a handle is what lets a system drive them - the file authors their
/// values instead, on the aspects it hangs on its volumes.
/// </summary>
sealed record Ambience(
    EffectHandle<GradeEffect> Grade,
    SurfaceFeatureHandle<DepthFogFeature> Fog,
    Entity RainEmitter) {

    // Fog and grade read their own smoothed state back from their handles. Neither an emitter's rate
    // nor its rotation can be read back into the intensity and lean that produced them, so the weather
    // keeps its own.
    public Rain Weather;
}
