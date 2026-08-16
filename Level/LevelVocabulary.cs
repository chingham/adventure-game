using System.Numerics;
using System.Text.Json;
using AdventureGame.App;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Meshes;
using Quark.Kit.Scenes.Files;
using Quark.Numerics;
using Quark.Physics.Dimension3D;
using Quark.Scenes;

namespace AdventureGame.Level;

/// <summary>
/// What a level file can say. Shape verbs place by the minimum corner of the footprint, like the lego
/// kit they replace; rider verbs speak in world coordinates and turn them into whatever the component
/// wants, so nothing in the file has to know where an origin ended up.
/// </summary>
sealed partial class LevelVocabulary {
    // Order inside one entity: the shape first (like the Kit's own mesh verb), then what needs a shape
    // to exist, then the riders - which may replace the body the shape left behind.
    const int Shape = 10;
    const int Body = 200;
    const int Rider = 300;

    public static void Register(SceneVocabulary vocabulary, GameMesh terrain) {
        var level = new LevelVocabulary();

        vocabulary
            // Procedural geometry the file cannot describe, named so it can still place it
            .Mesh("terrain", terrain)

            // Shapes
            .Verb("block", level.Block, Shape, schema: SizeSchema)
            .Verb("pillar", level.Pillar, Shape, payloadType: typeof(PillarPayload))
            .Verb("ramp", level.Ramp, Shape, payloadType: typeof(RampPayload))
            .Verb("stairs", level.Stairs, Shape, payloadType: typeof(StairsPayload))
            .Verb("trigger", level.Trigger, Shape, payloadType: typeof(TriggerPayload))
            .Verb("space", level.Space, Shape, payloadType: typeof(SpacePayload))
            .Verb("portalEnd", level.PortalEnd, Shape, payloadType: typeof(PortalEndPayload))

            // Bodies, for a shape the file did not build itself
            .Verb("solid", level.Solid, Body, schema: FlagSchema)

            // Riders
            .Verb("mover", level.Mover, Rider, payloadType: typeof(MoverPayload))
            .Verb("interactable", level.Interactable, Rider, payloadType: typeof(InteractablePayload))
            .Verb("sequence", level.Sequence, Rider, schema: SequenceSchema);
    }

    const string SizeSchema = """
        { "type": "array", "items": { "type": "number" }, "minItems": 3, "maxItems": 3 }
        """;

    const string FlagSchema = """{ "type": "boolean" }""";

    // Placing

    // Everything a shape verb does once it knows its mesh and where its origin lands: the corner the
    // file authored becomes an origin, and the riders that follow read that origin rather than the
    // corner - which is why it is written back onto the parse state.
    void Place(SceneEntityBuilder entity, SceneParseContext ctx, GameMesh mesh, Vector3d origin,
        Quaternion rotation = default) {
        entity.At(origin, rotation == default ? Quaternion.Identity : rotation);
        ctx.Entity.Position = origin;
        ctx.Entity.Mesh = mesh;

        if (mesh.Mesh is { } render)
            entity.Add(new RenderMesh { Mesh = render, Material = mesh.Material });
    }

    // A brick with no material of its own takes the file's greybox grey, by name like everything else
    static MaterialHandle Material(SceneParseContext ctx) =>
        ctx.Entity.Material.IsValid ? ctx.Entity.Material : ctx.Material("environment", ctx.Location);

    // Bodies

    static RigidBody Static(TransformedShape[] shapes) => new() {
        Kind = RigidBodyKind.Static,
        Layer = Layers.Environment,
        Shapes = shapes
    };

    static RigidBody Kinematic(TransformedShape[] shapes) => new() {
        Kind = RigidBodyKind.Kinematic,
        Layer = Layers.Environment,
        Shapes = shapes
    };

    static RigidBody TriggerVolume(TransformedShape[] shapes) => new() {
        Kind = RigidBodyKind.Static,
        IsTrigger = true,
        Layer = Layers.Trigger,
        Shapes = shapes
    };

    // The shape a rider rides. A rider with nothing under it is an authoring mistake worth naming.
    static GameMesh Ridden(SceneParseContext ctx, string verb) =>
        ctx.Entity.Mesh ?? throw ctx.Error(ctx.Location, $"'{verb}' needs a shape on the same entity.");
}
