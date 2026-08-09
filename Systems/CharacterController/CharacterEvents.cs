using Quark.Ecs;
using Quark.Numerics;

namespace AdventureGame.Systems.CharacterController;

public static class CharacterEvents {
    public readonly record struct Landed(Entity Entity, double ImpactSpeed);
    public readonly record struct Jumped(Entity Entity);

    // The character was moved discontinuously. Anything that trails behind it (camera rigs today,
    // particles or audio later) rebases itself by this transform instead of chasing across the map.
    // Door is the crossed portal end, for consumers that read what the far side asks of them.
    public readonly record struct Teleported(
        Entity Entity, Vector3d From, Vector3d To, double YawDelta, Entity Door);
}