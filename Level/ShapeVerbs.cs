using System.Numerics;
using System.Text.Json;
using Quark.Kit.Components;
using Quark.Kit.Scenes.Files;
using Quark.Numerics;
using Quark.Physics.Dimension3D;
using Quark.Physics.Dimension3D.Shapes;
using Quark.Scenes;
using Vector3 = System.Numerics.Vector3;

namespace AdventureGame.Level;

// The greybox kit: solid scenery, authored by the corner of its footprint.
partial class LevelVocabulary {
    // block: [w, d, h]
    void Block(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var size = SceneJson.Vec3d(json, ctx.Location);
        var mesh = ctx.Primitives.Box((Vector3)size, Material(ctx));

        Place(entity, ctx, mesh, ctx.Entity.Position + size * 0.5);
        entity.Add(Static(mesh.Shapes));
    }

    sealed class PillarPayload {
        public double R { get; set; } = 0.5;
        public double H { get; set; }
    }

    void Pillar(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<PillarPayload>(json, ctx.Location);
        var mesh = ctx.Primitives.Cylinder((float)p.R, (float)p.H, Material(ctx));

        Place(entity, ctx, mesh, ctx.Entity.Position + new Vector3d(p.R, p.R, p.H / 2));
        entity.Add(Static(mesh.Shapes));
    }

    sealed class RampPayload {
        public Vector3d Size { get; set; }      // across, run, rise
        public Facing Facing { get; set; }
    }

    void Ramp(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<RampPayload>(json, ctx.Location);
        var footprint = BrickGeometry.Footprint(p.Facing, (float)p.Size.X, (float)p.Size.Y);
        var mesh = ctx.Primitives.Wedge((Vector3)p.Size, Material(ctx));

        Place(entity, ctx, mesh,
            ctx.Entity.Position + new Vector3d(footprint.X / 2, footprint.Y / 2, p.Size.Z / 2),
            BrickGeometry.Rotation(p.Facing));
        entity.Add(Static(mesh.Shapes));
    }

    sealed class StairsPayload {
        public int Steps { get; set; }
        public double W { get; set; }
        public Facing Facing { get; set; }
    }

    // A flight is one entity: the treads are child meshes, and the body under them is a single compound
    // collider. A rigid body has to sit on a root entity, so the steps cannot carry one apiece.
    void Stairs(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        var p = ctx.Get<StairsPayload>(json, ctx.Location);
        if (p.Steps <= 0 || p.W <= 0)
            throw ctx.Error(ctx.Location, "Stairs need a positive 'steps' and 'w'.");

        var along = p.Steps * BrickGeometry.StepRun;
        var footprint = BrickGeometry.Footprint(p.Facing, (float)p.W, along);
        var center = new Vector2(footprint.X / 2, footprint.Y / 2);
        var direction = BrickGeometry.Direction(p.Facing);
        var material = Material(ctx);
        var shapes = new TransformedShape[p.Steps];

        for (var i = 0; i < p.Steps; i++) {
            var rise = (i + 1) * BrickGeometry.StepRise;

            // Each tread is one run deep, measured from the foot edge along the climb direction
            var offset = center + direction * ((i + 0.5f) * BrickGeometry.StepRun - along / 2);
            var size = p.Facing is Facing.North or Facing.South
                ? new Vector3((float)p.W, BrickGeometry.StepRun, rise)
                : new Vector3(BrickGeometry.StepRun, (float)p.W, rise);

            var local = new Vector3(offset.X, offset.Y, rise / 2);
            var tread = ctx.Primitives.Box(size, material);

            entity.Child($"step{i}", step => step
                .At(local)
                .Add(new RenderMesh { Mesh = tread.Mesh!, Material = tread.Material }));

            shapes[i] = new TransformedShape(new Box(size.X, size.Y, size.Z), Pose.At(local));
        }

        entity.Add(Static(shapes));
    }

    // solid: true - a body for a shape the file built with the Kit's own mesh verb
    void Solid(SceneEntityBuilder entity, JsonElement json, SceneParseContext ctx) {
        if (json.ValueKind == JsonValueKind.False)
            return;

        entity.Add(Static(Ridden(ctx, "solid").Shapes));
    }
}
