using System.Numerics;
using AdventureGame.Systems;
using AdventureGame.Systems.Camera;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Physics.Dimension3D;
using Quark.Physics.Dimension3D.Shapes;
using Quark.Kit.Rendering.Meshes;
using Quark.Numerics;

namespace AdventureGame.Library;

// Which way a ramp ascends or stairs climb.
enum Facing { North, East, South, West }   // +Y, +X, -Y, -X

// Lego-style greybox kit. Every brick is placed by the minimum corner of its footprint (x, y), grows
// toward +X/+Y and upward from the base altitude z; placements return the top altitude, so bricks stack
// by feeding one into the next. Material defaults to the environment grey.
sealed class Bricks(EntityCommands world, GamePrimitiveLibrary p, GreyboxMaterials g) {
    // Steps
    public const float StepRise = 0.25f;
    public const float StepRun = 0.5f;

    // Flat bricks

    public float Block(float x, float y, float w, float d, float h, float z = 0, MaterialHandle m = default) {
        Spawn(p.Box(new Vector3(w, d, h), Mat(m)), new Vector3d(x + w / 2, y + d / 2, z + h / 2));
        return z + h;
    }

    public float Pillar(float x, float y, float h, float radius = 0.5f, float z = 0, MaterialHandle m = default) {
        Spawn(p.Cylinder(radius, h, Mat(m)), new Vector3d(x + radius, y + radius, z + h / 2));
        return z + h;
    }

    // Climbing bricks

    public float Ramp(float x, float y, Facing facing, float w, float run, float h, float z = 0, MaterialHandle m = default) {
        var footprint = Footprint(facing, across: w, along: run);
        Spawn(
            p.Wedge(new Vector3(w, run, h), Mat(m)),
            new Vector3d(x + footprint.X / 2, y + footprint.Y / 2, z + h / 2),
            Yaw(facing));
        return z + h;
    }

    public float Stairs(float x, float y, Facing facing, int steps, float w, float z = 0, MaterialHandle m = default) {
        var along = steps * StepRun;
        var footprint = Footprint(facing, across: w, along: along);
        var center = new Vector2(x + footprint.X / 2, y + footprint.Y / 2);
        var dir = Direction(facing);

        for (var i = 0; i < steps; i++) {
            var h = (i + 1) * StepRise;
            // Each tread is one run deep, measured from the foot edge along the climb direction
            var step = center + dir * ((i + 0.5f) * StepRun - along / 2);
            var size = facing is Facing.North or Facing.South
                ? new Vector3(w, StepRun, h)
                : new Vector3(StepRun, w, h);
            Spawn(p.Box(size, Mat(m)), new Vector3d(step.X, step.Y, z + h / 2));
        }

        return z + steps * StepRise;
    }

    // Devices

    // Solid pad that sinks when stood on, plus the trigger volume above it that does the detecting.
    // Pressing emits `emit`; the pad pops back when "<emit>.done" comes home. The pad is static: its
    // few centimetres of travel never have to carry the rider.
    public float Button(float x, float y, float w, float d, float z = 0, string emit = "button") {
        const float padHeight = 0.2f;
        var cx = x + w / 2;
        var cy = y + d / 2;

        var pad = world.Spawn(p.Box(new Vector3(w, d, padHeight), g.Interactive))
            .At(new Vector3d(cx, cy, z + padHeight / 2))
            .Static(layer: Layers.Environment)
            .With(new LevelBrick())
            .Named($"button-pad:{emit}");

        world.Spawn()
            .At(new Vector3d(cx, cy, z + padHeight + 0.5))
            .Body(new RigidBody {
                Kind = RigidBodyKind.Static,
                IsTrigger = true,
                Layer = Layers.Trigger,
                Shapes = [new TransformedShape(new Box(w, d, 1f))]
            })
            .With(new Button { Emit = emit, Pad = pad, RestZ = z + padHeight / 2 })
            .With(new LevelBrick())
            .Named($"button:{emit}");

        return z + padHeight;
    }

    // Point the character can act on, with no body of its own: hand it the footprint of whatever it sits
    // on and it lands on top of it, in the middle. Using it emits `emit`, like a button does.
    public void Interact(float x, float y, float w, float d, float h, float z,
        float reach, string prompt, string emit, bool once) {
        world.Spawn()
            .At(new Vector3d(x + w / 2, y + d / 2, z + h))
            .With(new Interactable { Prompt = prompt, Emit = emit, Reach = reach, Once = once })
            .With(new LevelBrick())
            .Named($"interact:{emit}");
    }

    // Moving deck. Stops are footprint corners like every brick; the deck idles on the first stop.
    public Entity Platform(float w, float d, float h, Vector3d home, Vector3d stop, MovingPlatform route, MaterialHandle m = default) {
        var half = new Vector3d(w / 2, d / 2, h / 2);
        route.From = home + half;
        route.To = stop + half;

        return world.Spawn(p.Box(new Vector3(w, d, h), Mat(m)))
            .At(route.From)
            .Kinematic(layer: Layers.Environment)
            .With(route)
            .With(new LevelBrick())
            .Named("platform");
    }

    // One portal end: corner of its door footprint and the direction the character faces on arrival.
    public readonly record struct PortalAnchor(float X, float Y, float Z, Facing Arrive);

    const float PortalDepth = 0.5f;

    // Two-way link: each end is a thin trigger volume carrying the frame mapping toward the other.
    // Crossing preserves the transform relative to the door, so both ends together behave like one
    // continuous doorway.
    public void Portal(float w, float h, PortalAnchor a, PortalAnchor b) {
        SpawnPortalEnd(w, h, a, MappingOf(w, a, b));
        SpawnPortalEnd(w, h, b, MappingOf(w, b, a));
    }

    void SpawnPortalEnd(float w, float h, PortalAnchor end, Portal mapping) {
        var footprint = Footprint(end.Arrive, across: w, along: PortalDepth);
        world.Spawn()
            .At(new Vector3d(end.X + footprint.X / 2, end.Y + footprint.Y / 2, end.Z + h / 2))
            .Body(new RigidBody {
                Kind = RigidBodyKind.Static,
                IsTrigger = true,
                Layer = Layers.Trigger,
                Shapes = [new TransformedShape(new Box(footprint.X, footprint.Y, h))]
            })
            .With(mapping)
            .With(new LevelBrick())
            .Named("portal");
    }

    // Frame mapping for one crossing direction. Entering `from` means moving against its arrival
    // direction, hence the extra half-turn in the yaw delta.
    static Portal MappingOf(float w, PortalAnchor from, PortalAnchor to) {
        var direction = Direction(from.Arrive);
        return new Portal {
            Center = DoorCenter(w, from),
            Through = new Vector3d(-direction.X, -direction.Y, 0),
            ExitCenter = DoorCenter(w, to),
            YawDelta = Utils.WrapAngle(YawOf(to.Arrive) - YawOf(from.Arrive) - Math.PI),
            HalfWidth = w / 2,
            HalfDepth = PortalDepth / 2
        };
    }

    static Vector3d DoorCenter(float w, PortalAnchor end) {
        var footprint = Footprint(end.Arrive, across: w, along: PortalDepth);
        return new Vector3d(end.X + footprint.X / 2, end.Y + footprint.Y / 2, end.Z);
    }

    // Camera spaces

    // Invisible volume that sets the mood of a place: how far the isometric shot sits, how wide it
    // is, whether it rides the character or holds still on the room's middle, and its fog.
    public void Space(float x, float y, float z, float w, float d, float h,
        float distance, float fov, bool centerPivot, float? fog = null, string? id = null) {
        var min = new Vector3d(x, y, z);
        world.Spawn()
            .At(min + new Vector3d(w, d, h) * 0.5)
            .With(new Space {
                Min = min,
                Max = min + new Vector3d(w, d, h),
                Distance = distance,
                FieldOfView = fov,
                CenterPivot = centerPivot,
                Fog = fog
            })
            .With(new LevelBrick())
            .Named(id ?? "space");
    }

    // Trigger volumes

    // Overlap-only volume: no contacts, it just reports who is inside. Rendered while zones are being
    // authored - swap to WithoutMesh once they carry real behaviour.
    public float Zone(float x, float y, float w, float d, float h, float z = 0, string? id = null) {
        world.Spawn(p.Box(new Vector3(w, d, h), g.Interactive))
            .At(new Vector3d(x + w / 2, y + d / 2, z + h / 2))
            .Body(new RigidBody { Kind = RigidBodyKind.Static, IsTrigger = true, Layer = Layers.Trigger })
            .With(new LevelBrick())
            .Named(id ?? "zone");
        return z + h;
    }

    // Helpers

    MaterialHandle Mat(MaterialHandle m) => m.IsValid ? m : g.Environment;

    void Spawn(GameMesh mesh, Vector3d position, Quaternion rotation = default) =>
        world.Spawn(mesh).At(position, rotation).Static(layer: Layers.Environment).With(new LevelBrick());

    // Ground extent once turned toward the facing: across and along swap on the east-west axis.
    static Vector2 Footprint(Facing f, float across, float along) =>
        f is Facing.North or Facing.South ? new Vector2(across, along) : new Vector2(along, across);

    // Character yaw convention: Atan2(dir.X, dir.Y), so North = 0 and East = PI / 2
    static double YawOf(Facing f) => f switch {
        Facing.North => 0,
        Facing.East => Math.PI / 2,
        Facing.South => Math.PI,
        _ => -Math.PI / 2
    };

    static Vector2 Direction(Facing f) => f switch {
        Facing.North => new(0, 1),
        Facing.East => new(1, 0),
        Facing.South => new(0, -1),
        _ => new(-1, 0)
    };

    static Quaternion Yaw(Facing f) => f switch {
        Facing.North => Quaternion.Identity,
        Facing.East => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -MathF.PI / 2),
        Facing.South => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI),
        _ => Quaternion.CreateFromAxisAngle(Vector3.UnitZ, MathF.PI / 2)
    };
}
