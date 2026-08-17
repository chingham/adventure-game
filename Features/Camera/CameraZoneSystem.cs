using Quark.Ecs;
using Quark.Kit;
using Quark.Numerics;

namespace AdventureGame.Features.Camera;

// Frames wherever the character stands: the wide shot of the zone he is in, and whether that shot is
// the interior one. DoorApproachSystem owns the doorway rig and leaves this one alone.
sealed class CameraZoneSystem : ISystem {
    // Framing with no camera zone at all - the wide default the rig was authored with. The level says
    // it itself as soon as it authors a world-level camera aspect.
    const double OpenDistance = 90;
    const double OpenFieldOfView = 0.4;
    const double FocusHeight = 1.3;

    const double DecayRate = 6;

    // Past this much movement in one step the pivot is not travelling, it jumped: snap rather than
    // sweep the world. Adjacent zones stay under it and still blend.
    const double JumpDistance = 50;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (!Zones.TryListener(world, out var position))
            return;

        var zone = Zones.Resolve<CameraZone>(world, position);

        foreach (var row in world.Query<CameraDirector>()) {
            ref var director = ref row.Component1;

            // Interior is what the zone says, not the mere fact of being in one: an outdoor zone can
            // change the weather without swinging the camera round.
            director.InSpace = zone?.Aspect.Iso == true ? 1 : 0;

            var distance = zone?.Aspect.Distance ?? OpenDistance;
            var fieldOfView = zone?.Aspect.FieldOfView ?? OpenFieldOfView;
            var pivot = zone is { Aspect.CenterPivot: true, Volume: { } room }
                ? room.Center
                : position + new Vector3d(0, 0, FocusHeight);

            ref var iso = ref world.Get<CameraRig>(director.IsometricRig);
            iso.Distance = Decay.ExpDecay(iso.Distance, distance, DecayRate, deltaTime);
            iso.FieldOfView = Decay.ExpDecay(iso.FieldOfView, fieldOfView, DecayRate, deltaTime);
            iso.Pivot = Vector3d.Distance(iso.Pivot, pivot) > JumpDistance
                ? pivot
                : Decay.ExpDecay(iso.Pivot, pivot, DecayRate, deltaTime);

            // A near plane authored for a 90 m shot slices a 20 m one in half, so it follows.
            iso.NearPlane = Math.Clamp(iso.Distance / 12, 0.1, 10);
        }
    }
}
