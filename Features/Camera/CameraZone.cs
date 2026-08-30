using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Features.Camera;

// How a place is framed, and whether that framing is the interior one. It rides a volume like any
// other aspect, or stands alone as the level's own - the open air.
struct CameraZone {
    public bool Iso;
    public double Distance;
    public double FieldOfView;
    public bool CenterPivot;
}

// The listener
//
// Where the world around the player is judged from: whoever the camera follows, and the shot watching
// him. Read through the rig rather than from the character directly, so what the player sees and what
// the level does around him agree on one position.
static class CameraListener {
    public static bool TryResolve(World world, out Vector3d point, out CameraDirector shot) {
        foreach (var row in world.Query<CameraDirector>()) {
            if (!world.TryGet<FollowRig>(row.Component1.FollowRig, out var follow))
                continue;
            if (!world.TryGet<RelativeTransform>(follow.Target, out var target))
                continue;

            point = target.LocalTransform.Position;
            shot = row.Component1;
            return true;
        }

        point = default;
        shot = default;
        return false;
    }
}
