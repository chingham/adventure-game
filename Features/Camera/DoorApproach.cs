using AdventureGame.Common;
using AdventureGame.Features.Character;
using AdventureGame.Features.Portals;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Features.Camera;

// Turns the character's distance to a door into the camera transition, with no state of its own: the
// player scrubs the whole thing by walking. Approaching raises t, backing off lowers it, standing
// still freezes it - so a run crosses over fast and a careful step crosses slowly, for free.
//
// At full approach the doorway rig frames only the few metres around the door, which are duplicated
// on both sides, so the jump itself falls outside the frame. The rule that keeps a cut invisible:
// 2 * Distance * tan(fov / 2) must stay under the width of that duplicated ground.
sealed class DoorApproachSystem(CameraTuning tuning) : ISystem {
    readonly EventReader<CharacterEvents.Teleported> teleports = new();

    public void Update(World world, EntityCommands commands, float deltaTime) {
        CrossDoor(world);

        foreach (var row in world.Query<CameraDirector>()) {
            ref var director = ref row.Component1;

            if (!world.Has<FollowRig>(director.FollowRig))
                continue;

            ref var follow = ref world.Get<FollowRig>(director.FollowRig);
            // The transform, not the movement: the camera follows it too, so both stay coherent on
            // the frame a teleport lands.
            if (!world.TryGet<RelativeTransform>(follow.Target, out var target))
                continue;

            var position = target.LocalTransform.Position;
            var (closeness, doorYaw) = NearestDoor(world, position);

            director.Approach = Utils.SmoothStep01(closeness);
            follow.DoorWeight = director.Approach;
            follow.DoorYaw = doorYaw;
            world.Get<CameraRig>(director.DoorRig).Pivot = position + new Vector3d(0, 0, tuning.FocusHeight);
        }
    }

    // On a crossing, adopt the base shot the destination sits in and turn the fixed rigs by the same
    // angle as the world. Both are snapped: the doorway rig owns the image at that moment, and it
    // frames the two sides identically, so nothing of this shows. The arrival point comes from the
    // event rather than the transform, which only catches up a frame later.
    void CrossDoor(World world) {
        foreach (var evt in teleports.Read(world))
            foreach (var row in world.Query<CameraDirector>()) {
                ref var director = ref row.Component1;
                director.InSpace = SpaceSystem.Find(world, evt.To) is null ? 0 : 1;
                director.SnapTarget(director.Flipped ? 1 - director.InSpace : director.InSpace);

                ref var iso = ref world.Get<CameraRig>(director.IsometricRig);
                ref var door = ref world.Get<CameraRig>(director.DoorRig);
                iso.Yaw = Utils.WrapAngle(iso.Yaw + evt.YawDelta);
                door.Yaw = Utils.WrapAngle(door.Yaw + evt.YawDelta);
            }
    }

    // Closeness to the nearest door, 0 outside its reach and 1 in the opening, plus the yaw that
    // door wants the camera at - looking along the way through it, which puts the camera on the
    // character's side of the wall whichever way he is going. Symmetric across the threshold, so
    // walking away on the far side plays the same ramp backwards.
    (double Closeness, double Yaw) NearestDoor(World world, Vector3d position) {
        var closest = 0.0;
        var yaw = 0.0;

        foreach (var row in world.Query<Portal>()) {
            var portal = row.Component1;
            var toDoor = position - portal.Center;
            if (Math.Abs(toDoor.Z) > tuning.VerticalReach)
                continue;

            var along = Vector3d.Dot(toDoor, portal.Through);
            var lateral = Utils.FlattenXY(toDoor - portal.Through * along).Length();

            var axial = 1 - Math.Max(0, Math.Abs(along) - tuning.DoorPlateau) / (tuning.ApproachDepth - tuning.DoorPlateau);
            var sideways = 1 - Math.Max(0, lateral - portal.HalfWidth) / tuning.LateralFalloff;
            var closeness = Utils.Clamp01(axial) * Utils.Clamp01(sideways);

            if (closeness <= closest)
                continue;
            closest = closeness;
            yaw = Math.Atan2(portal.Through.X, portal.Through.Y);
        }

        return (closest, yaw);
    }
}
