using System.Numerics;
using AdventureGame.App;
using AdventureGame.Common;
using AdventureGame.Features.Camera;
using AdventureGame.Features.Interaction;
using AdventureGame.Features.Platforms;
using AdventureGame.Features.Portals;
using AdventureGame.Features.Sequences;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Meshes;
using Quark.Numerics;
using Quark.Physics.Dimension3D;
using Quark.Physics.Dimension3D.Shapes;

namespace AdventureGame.Level;

// Which way a ramp ascends or stairs climb.
enum Facing { North, East, South, West }   // +Y, +X, -Y, -X

// Whether an object draws, and what its body does. Both defaults are the common case, so a plain
// `default` shell is the visible solid every piece of scenery wants.
enum Look { Visible, Hidden }
enum Solidity { Solid, Trigger, Moving, None }

readonly record struct Shell(Look Look = Look.Visible, Solidity Solidity = Solidity.Solid);

// Lego-style greybox kit. Every object is placed by the minimum corner of its footprint (x, y), grows
// toward +X/+Y and upward from the base altitude z. Shape verbs return the entity they spawned so the
// modifiers below can ride it. Material defaults to the environment grey.
sealed class Bricks(EntityCommands world, GamePrimitiveLibrary p, GreyboxMaterials g) {
    // Steps
    public const float StepRise = 0.25f;
    public const float StepRun = 0.5f;

    // Flat shapes

    public Entity Block(float x, float y, float w, float d, float h, float z = 0,
        MaterialHandle m = default, Shell shell = default) =>
        Spawn(p.Box(new Vector3(w, d, h), Mat(m)),
            new Vector3d(x + w / 2, y + d / 2, z + h / 2), default, shell);

    public Entity Pillar(float x, float y, float h, float radius = 0.5f, float z = 0,
        MaterialHandle m = default, Shell shell = default) =>
        Spawn(p.Cylinder(radius, h, Mat(m)),
            new Vector3d(x + radius, y + radius, z + h / 2), default, shell);

    // A place and nothing else - no mesh, no body. For a modifier that wants a spot rather than an
    // object: an interaction point on a doorway, a marker on the ground.
    public Entity Point(float x, float y, float z = 0) =>
        world.Spawn().At(new Vector3d(x, y, z)).With(new LevelBrick());

    // Climbing shapes

    public Entity Ramp(float x, float y, Facing facing, float w, float run, float h, float z = 0,
        MaterialHandle m = default, Shell shell = default) {
        var footprint = Footprint(facing, across: w, along: run);
        return Spawn(p.Wedge(new Vector3(w, run, h), Mat(m)),
            new Vector3d(x + footprint.X / 2, y + footprint.Y / 2, z + h / 2), Yaw(facing), shell);
    }

    public void Stairs(float x, float y, Facing facing, int steps, float w, float z = 0, MaterialHandle m = default) {
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
            Spawn(p.Box(size, Mat(m)), new Vector3d(step.X, step.Y, z + h / 2), default, default);
        }
    }

    // Devices

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
            YawDelta = Angle.Wrap(YawOf(to.Arrive) - YawOf(from.Arrive) - Math.PI),
            HalfWidth = w / 2,
            HalfDepth = PortalDepth / 2
        };
    }

    static Vector3d DoorCenter(float w, PortalAnchor end) {
        var footprint = Footprint(end.Arrive, across: w, along: PortalDepth);
        return new Vector3d(end.X + footprint.X / 2, end.Y + footprint.Y / 2, end.Z);
    }

    // Modifiers - behaviour bolted onto an object the shape verbs just spawned

    public void Name(Entity entity, string id) => world.Add(entity, new Name(id));

    // Overlap volume wired to the bus. `rearm` is the signal that unlatches it after an entry, so a
    // one-shot volume can wait for whatever it summoned to come home.
    public void Trigger(Entity entity, string? enter, string? exit = null, string? rearm = null) =>
        world.Add(entity, new Trigger { Enter = enter, Exit = exit, Rearm = rearm });

    // Round trip between where the object stands and one far stop, both already resolved to origins.
    // A shuttle leaves on its own; a called one waits for its signal and reports "<on>.done" home.
    public void Mover(Entity entity, Vector3d from, Vector3d to,
        PlatformMode mode, float speed, float dwell, string? on) =>
        world.Add(entity, new MovingPlatform {
            From = from,
            To = to,
            Mode = mode,
            Speed = speed,
            Dwell = dwell,
            On = on
        });

    // Point on the object the character can act on. The offset is measured from the entity's own
    // origin and turns with it, so it rides anything, still or moving.
    public void Interactable(Entity entity, Vector3d offset, string prompt, string emit,
        float reach, bool once) =>
        world.Add(entity, new Interactable {
            Offset = offset,
            Prompt = prompt,
            Emit = emit,
            Reach = reach,
            Once = once
        });

    // Invisible volume that sets the mood of a place: how far the isometric shot sits, how wide it
    // is, whether it rides the character or holds still on the room's middle, and its fog.
    public void Space(Entity entity, Vector3d min, Vector3d max,
        float distance, float fov, bool centerPivot, float? fog) =>
        world.Add(entity, new Space {
            Min = min,
            Max = max,
            Distance = distance,
            FieldOfView = fov,
            CenterPivot = centerPivot,
            Fog = fog
        });

    // Rules

    // A sequence has nowhere to stand in the world; it lives as an entity only so a reload sweeps it
    // away with everything else the file built.
    public void Sequence(string on, Step[] steps) =>
        world.Spawn(new SequenceDefinition { On = on, Steps = steps }).With(new LevelBrick());

    // Helpers

    MaterialHandle Mat(MaterialHandle m) => m.IsValid ? m : g.Environment;

    // The shell decides what survives of the primitive: its mesh, its collider, or only its place.
    Entity Spawn(GameMesh mesh, Vector3d position, Quaternion rotation, Shell shell) {
        var spawn = world
            .Spawn(shell.Look == Look.Hidden ? mesh.WithoutMesh() : mesh)
            .At(position, rotation);

        spawn = shell.Solidity switch {
            Solidity.Solid => spawn.Static(layer: Layers.Environment),
            Solidity.Trigger => spawn.Body(new RigidBody {
                Kind = RigidBodyKind.Static,
                IsTrigger = true,
                Layer = Layers.Trigger
            }),
            Solidity.Moving => spawn.Kinematic(layer: Layers.Environment),
            _ => spawn
        };

        return spawn.With(new LevelBrick());
    }

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
