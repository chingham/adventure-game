using System.Text.Json;
using AdventureGame.Features.Ambience;
using AdventureGame.Features.Camera;
using AdventureGame.Features.Interaction;
using AdventureGame.Features.Platforms;
using AdventureGame.Features.Portals;
using Quark.Kit.Scenes.Files;
using System.Numerics;
using Quark.Numerics;
using Quark.Physics.Dimension3D;
using Quark.Physics.Dimension3D.Shapes;
using Quark.Scenes;
using Vector3 = System.Numerics.Vector3;

namespace AdventureGame.Level;

// Everything in the level that does something: volumes that listen, doorways, platforms that move,
// points to act on, and the rules tying them together.
partial class LevelVocabulary {
    // Volumes

    sealed class TriggerPayload {
        public Vector3d Size { get; set; }
        public string? Enter { get; set; }
        public string? Exit { get; set; }
        public string? Rearm { get; set; }
    }

    // An overlap volume is its own shape: it never draws and never blocks, so there is no solid form
    // of it to contradict.
    void Trigger(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<TriggerPayload>(json, ctx.Location);
        if (p.Enter is null && p.Exit is null)
            throw ctx.Error(ctx.Location, "A trigger needs 'enter' or 'exit'.");

        entity.At(ctx.Entity.Position + p.Size * 0.5);
        entity.Add(TriggerVolume([new TransformedShape(
            new Box((float)p.Size.X, (float)p.Size.Y, (float)p.Size.Z))]));
        entity.Add(new Trigger { Enter = p.Enter, Exit = p.Exit, Rearm = p.Rearm });
    }

    // Zones
    //
    // A volume is a box and nothing else; the aspects riding it say what changes inside. An aspect
    // authored without a volume is the level's own - the open air.

    // volume: [w, d, h] - no mesh, no body, only a place
    void VolumeVerb(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var size = SceneJson.Vec3d(json, ctx.Location);
        var min = ctx.Entity.Position;

        entity.At(min + size * 0.5);
        entity.Add(new Volume { Min = min, Max = min + size });
    }

    sealed class CameraPayload {
        public bool Iso { get; set; }
        public double Distance { get; set; } = 90;
        public double Fov { get; set; } = 0.4;
        public string? Pivot { get; set; }
    }

    void CameraVerb(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<CameraPayload>(json, ctx.Location);
        if (p.Pivot is not (null or "follow" or "center"))
            throw ctx.Error(ctx.Location, $"Unknown pivot '{p.Pivot}'. Valid: follow, center.");

        entity.Add(new CameraZone {
            Iso = p.Iso,
            Distance = p.Distance,
            FieldOfView = p.Fov,
            CenterPivot = p.Pivot == "center"
        });
    }

    // Ambience
    //
    // One verb for the whole feel of a place, each part of it optional: what the file leaves out is
    // what the air around the volume keeps saying.

    sealed class FogPayload {
        public double Density { get; set; } = 0.02;
        public Vector4 Color { get; set; } = new(0.05f, 0.06f, 0.08f, 1);
    }

    sealed class GradePayload {
        public double Saturation { get; set; } = 1;
        public double Warmth { get; set; }
    }

    sealed class RainPayload {
        public double Intensity { get; set; }
        public double Tilt { get; set; } = 8;
    }

    void AmbienceVerb(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        if (json.ValueKind != JsonValueKind.Object)
            throw ctx.Error(ctx.Location, "'ambience' takes an object.");

        var zone = new AmbienceZone();

        if (Part<FogPayload>(json, ctx, "fog") is { } fog)
            zone.Fog = new Fog { Density = fog.Density, Color = fog.Color };

        if (Part<GradePayload>(json, ctx, "grade") is { } grade)
            zone.Grade = new Grade { Saturation = grade.Saturation, Warmth = grade.Warmth };

        if (Part<RainPayload>(json, ctx, "rain") is { } rain) {
            if (rain.Intensity is < 0 or > 1)
                throw ctx.Error($"{ctx.Location}.rain", "Rain intensity runs from 0 to 1.");

            zone.Rain = new Rain { Intensity = rain.Intensity, Tilt = rain.Tilt };
        }

        entity.Add(zone);
    }

    // The binder reflects one object deep, so the sub-blocks are read one at a time rather than as
    // members of an ambience payload.
    static T? Part<T>(JsonElement json, SceneParseContext ctx, string name) where T : class, new() =>
        json.TryGetProperty(name, out var part) ? ctx.Get<T>(part, $"{ctx.Location}.{name}") : null;

    const string AmbienceSchema = """
        {
          "type": "object",
          "properties": {
            "fog": {
              "type": "object",
              "properties": {
                "density": { "type": "number" },
                "color": { "$ref": "#/$defs/color4" }
              },
              "additionalProperties": false
            },
            "grade": {
              "type": "object",
              "properties": {
                "saturation": { "type": "number" },
                "warmth": { "type": "number" }
              },
              "additionalProperties": false
            },
            "rain": {
              "type": "object",
              "properties": {
                "intensity": { "type": "number" },
                "tilt": { "type": "number" }
              },
              "additionalProperties": false
            }
          },
          "additionalProperties": false
        }
        """;

    sealed class ShroudPayload {
        public double Edge { get; set; } = 1;
        public Vector4 Color { get; set; }
    }

    // Authored ahead of what draws it: the volume and its softness are level data, and pinning them now
    // means the day the effect exists no level file has to change.
    void ShroudVerb(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<ShroudPayload>(json, ctx.Location);
        entity.Add(new Shroud { Edge = p.Edge, Color = p.Color });
    }

    // Doorways

    sealed class PortalEndPayload {
        public double W { get; set; }
        public double H { get; set; }
        public Facing Facing { get; set; }

        // Where this end lands, flat rather than nested: the binder reads one object deep
        public Vector3d ExitAt { get; set; }
        public Facing ExitFacing { get; set; }
    }

    // One end of a two-way link, and the frame mapping toward the other: crossing preserves the
    // transform relative to the door, so both ends together behave like one continuous doorway. Each
    // end is its own entity because each carries its own body.
    void PortalEnd(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<PortalEndPayload>(json, ctx.Location);
        var footprint = BrickGeometry.Footprint(p.Facing, (float)p.W, BrickGeometry.PortalDepth);
        var corner = ctx.Entity.Position;

        entity.At(corner + new Vector3d(footprint.X / 2, footprint.Y / 2, p.H / 2));
        entity.Add(TriggerVolume([new TransformedShape(
            new Box(footprint.X, footprint.Y, (float)p.H))]));

        // Entering this end means moving against its arrival direction, hence the half-turn in the yaw
        var direction = BrickGeometry.Direction(p.Facing);
        entity.Add(new Portal {
            Center = DoorCenter(p.W, corner, p.Facing),
            Through = new Vector3d(-direction.X, -direction.Y, 0),
            ExitCenter = DoorCenter(p.W, p.ExitAt, p.ExitFacing),
            YawDelta = Angle.Wrap(
                BrickGeometry.YawOf(p.ExitFacing) - BrickGeometry.YawOf(p.Facing) - Math.PI),
            HalfWidth = p.W / 2,
            HalfDepth = BrickGeometry.PortalDepth / 2
        });
    }

    static Vector3d DoorCenter(double w, Vector3d corner, Facing facing) {
        var footprint = BrickGeometry.Footprint(facing, (float)w, BrickGeometry.PortalDepth);
        return corner + new Vector3d(footprint.X / 2, footprint.Y / 2, 0);
    }

    // Riders

    sealed class MoverPayload {
        public Vector3d To { get; set; }          // where the object's origin stands at the far stop
        public PlatformMode Mode { get; set; }
        public string? On { get; set; }
        public double Speed { get; set; } = 6;
        public double Dwell { get; set; } = 1;
    }

    // A round trip. The body has to become kinematic, which is why this runs after the shape verb that
    // made it static.
    void Mover(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<MoverPayload>(json, ctx.Location);
        if (p.Mode is PlatformMode.Called && string.IsNullOrEmpty(p.On))
            throw ctx.Error(ctx.Location, "A called mover needs 'on'.");

        entity.Add(Kinematic(Ridden(ctx, "mover").Shapes));
        entity.Add(new MovingPlatform {
            From = ctx.Entity.Position,
            To = p.To,
            Mode = p.Mode,
            Speed = (float)p.Speed,
            Dwell = (float)p.Dwell,
            On = p.On
        });
    }

    sealed class InteractablePayload {
        public Vector3d At { get; set; }          // where the hand lands, in the world
        public string Prompt { get; set; } = "Use";
        public string? Emit { get; set; }
        public double Reach { get; set; } = 1.8;
        public bool Once { get; set; }
    }

    void Interactable(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<InteractablePayload>(json, ctx.Location);
        if (string.IsNullOrEmpty(p.Emit))
            throw ctx.Error(ctx.Location, "An interactable needs 'emit'.");
        if (p.Reach <= 0)
            throw ctx.Error(ctx.Location, "Reach must be positive.");

        // Stored from the entity's own origin so it turns and travels with whatever carries it
        entity.Add(new Interactable {
            Offset = p.At - ctx.Entity.Position,
            Prompt = p.Prompt,
            Emit = p.Emit,
            Reach = p.Reach,
            Once = p.Once
        });
    }
}
