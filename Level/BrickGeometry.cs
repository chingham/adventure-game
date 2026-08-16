using System.Numerics;

namespace AdventureGame.Level;

// Which way a ramp ascends, stairs climb, or a doorway is faced on arrival.
enum Facing { North, East, South, West }   // +Y, +X, -Y, -X

// The grid the level is authored on: every brick is placed by the minimum corner of its footprint,
// grows toward +X/+Y and upward from that corner. The verbs turn a corner into an entity origin;
// this is the arithmetic they share.
static class BrickGeometry {
    // Steps
    public const float StepRise = 0.25f;
    public const float StepRun = 0.5f;

    // How thick a doorway is, along the way through it
    public const float PortalDepth = 0.5f;

    // Ground extent once turned toward the facing: across and along swap on the east-west axis.
    public static Vector2 Footprint(Facing f, float across, float along) =>
        f is Facing.North or Facing.South ? new Vector2(across, along) : new Vector2(along, across);

    // Character yaw convention: Atan2(dir.X, dir.Y), so North = 0 and East = PI / 2
    public static double YawOf(Facing f) => f switch {
        Facing.North => 0,
        Facing.East => Math.PI / 2,
        Facing.South => Math.PI,
        _ => -Math.PI / 2
    };

    public static Vector2 Direction(Facing f) => f switch {
        Facing.North => new(0, 1),
        Facing.East => new(1, 0),
        Facing.South => new(0, -1),
        _ => new(-1, 0)
    };

    public static Quaternion Rotation(Facing f) => f switch {
        Facing.North => Quaternion.Identity,
        Facing.East => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathF.PI / 2),
        Facing.South => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI),
        _ => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2)
    };
}
