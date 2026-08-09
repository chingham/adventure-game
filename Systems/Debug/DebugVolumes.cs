using System.Numerics;
using AdventureGame.Systems.Camera;
using Quark.Ecs;
using Quark.Graphics.GeometryProcessors;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Numerics;
using Quark.Physics.Dimension3D;
using Quark.Physics.Dimension3D.Shapes;
using Quark.Platform.Input;

namespace AdventureGame.Systems;

// Draws what the level hides: colliders carrying no mesh, camera spaces, interaction points. Everything
// goes to the immediate line stream, so none of it survives the frame. Placing a volume blind is what
// would slow authoring down - this is the eye for it.
sealed class DebugVolumeSystem(DefaultRenderingModule rendering, IInput input) : ISystem {
    // A colour per family, so a glance says what a volume is
    static readonly uint TriggerHue = Rgba(255, 205, 60);
    static readonly uint ColliderHue = Rgba(255, 105, 70);
    static readonly uint SpaceHue = Rgba(80, 230, 255);
    static readonly uint PointHue = Rgba(120, 255, 140);
    static readonly uint RetiredHue = Rgba(120, 255, 140, 70);

    const double CrossArm = 0.15;
    const int RingSegments = 24;

    bool shown;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (input.Button(Controls.ToggleVolumes) == ButtonState.JustPressed)
            shown = !shown;

        // The only writer of the line stream, so it owns the clear
        var lines = rendering.DebugProcessor;
        lines.Clear();
        if (!shown)
            return;

        // Bodies with nothing to show for themselves: trigger volumes, and solids stripped of their mesh
        foreach (var row in world.Query<RigidBody, RelativeTransform>()) {
            if (world.Has<RenderMesh>(row.Entity) || row.Component1.Shapes is not { Length: > 0 } shapes)
                continue;

            var pose = row.Component2.LocalTransform;
            var hue = row.Component1.IsTrigger ? TriggerHue : ColliderHue;
            foreach (var shape in shapes)
                Wire(lines, shape.Shape, Frame.Of(pose, shape.Transform), hue);
        }

        // Camera spaces, which are pure volume - already axis-aligned and in world coordinates
        foreach (var row in world.Query<Space>()) {
            var space = row.Component1;
            var half = (space.Max - space.Min) * 0.5;
            WireBox(lines, Frame.World(space.Center), -half, half, SpaceHue);
        }

        // Interaction points: where the hand lands, and how far it reaches
        foreach (var row in world.Query<Interactable, RelativeTransform>()) {
            var interactable = row.Component1;
            var pose = row.Component2.LocalTransform;
            var point = pose.Position + Vector3d.Transform(interactable.Offset, pose.Rotation);
            var hue = interactable.Enabled ? PointHue : RetiredHue;

            Cross(lines, point, hue);
            Arc(lines, Frame.World(point), Vector3d.Zero, interactable.Reach,
                Vector3d.UnitX, Vector3d.UnitY, 1, hue);
        }
    }

    // Wires, one per shape the physics knows

    static void Wire(DataStreamGeometryProcessor lines, IShape shape, Frame frame, uint color) {
        switch (shape) {
            case Box box:
                var half = new Vector3d(box.Width, box.Height, box.Depth) * 0.5;
                WireBox(lines, frame, -half, half, color);
                break;
            case Capsule capsule:
                WireCapsule(lines, frame, capsule.Radius, capsule.Height, color);
                break;
            case Cylinder cylinder:
                WireCylinder(lines, frame, cylinder.Radius, cylinder.Height, color);
                break;
            case Sphere sphere:
                WireSphere(lines, frame, sphere.Radius, color);
                break;
            case ConvexHull hull:
                WireHull(lines, frame, hull, color);
                break;
            default:
                // Meshes and anything added later: the box its own support function reports
                var (min, max) = Extent(shape);
                WireBox(lines, frame, min, max, color);
                break;
        }
    }

    static void WireBox(DataStreamGeometryProcessor lines, Frame f, Vector3d min, Vector3d max, uint color) {
        Span<Vector3d> corner = stackalloc Vector3d[8];
        for (var i = 0; i < 8; i++)
            corner[i] = f.At(new Vector3d(
                (i & 1) == 0 ? min.X : max.X,
                (i & 2) == 0 ? min.Y : max.Y,
                (i & 4) == 0 ? min.Z : max.Z));

        foreach (var (a, b) in BoxEdges)
            Line(lines, corner[a], corner[b], color);
    }

    // Corner bits are x, then y, then z, so an edge joins two that differ by a single bit
    static readonly (int A, int B)[] BoxEdges = [
        (0, 1), (1, 3), (3, 2), (2, 0),
        (4, 5), (5, 7), (7, 6), (6, 4),
        (0, 4), (1, 5), (2, 6), (3, 7)
    ];

    // Radius rings at both ends of the segment, plus a half turn over each pole on two planes
    static void WireCapsule(DataStreamGeometryProcessor lines, Frame f, double radius, double height, uint color) {
        var top = Vector3d.UnitZ * (height / 2);
        var bottom = -top;

        Arc(lines, f, top, radius, Vector3d.UnitX, Vector3d.UnitY, 1, color);
        Arc(lines, f, bottom, radius, Vector3d.UnitX, Vector3d.UnitY, 1, color);
        Arc(lines, f, top, radius, Vector3d.UnitX, Vector3d.UnitZ, 0.5, color);
        Arc(lines, f, top, radius, Vector3d.UnitY, Vector3d.UnitZ, 0.5, color);
        Arc(lines, f, bottom, radius, Vector3d.UnitX, -Vector3d.UnitZ, 0.5, color);
        Arc(lines, f, bottom, radius, Vector3d.UnitY, -Vector3d.UnitZ, 0.5, color);
        Sides(lines, f, radius, top, bottom, color);
    }

    static void WireCylinder(DataStreamGeometryProcessor lines, Frame f, double radius, double height, uint color) {
        var top = Vector3d.UnitZ * (height / 2);

        Arc(lines, f, top, radius, Vector3d.UnitX, Vector3d.UnitY, 1, color);
        Arc(lines, f, -top, radius, Vector3d.UnitX, Vector3d.UnitY, 1, color);
        Sides(lines, f, radius, top, -top, color);
    }

    static void WireSphere(DataStreamGeometryProcessor lines, Frame f, double radius, uint color) {
        Arc(lines, f, Vector3d.Zero, radius, Vector3d.UnitX, Vector3d.UnitY, 1, color);
        Arc(lines, f, Vector3d.Zero, radius, Vector3d.UnitX, Vector3d.UnitZ, 1, color);
        Arc(lines, f, Vector3d.Zero, radius, Vector3d.UnitY, Vector3d.UnitZ, 1, color);
    }

    // A hull carries no edge list, only the points it is spanned by - so show those rather than invent faces
    static void WireHull(DataStreamGeometryProcessor lines, Frame f, ConvexHull hull, uint color) {
        foreach (var point in hull.Points)
            Cross(lines, f.At(new Vector3d(point.X, point.Y, point.Z)), color);
    }

    // The four silhouette lines joining two rings
    static void Sides(DataStreamGeometryProcessor lines, Frame f, double radius, Vector3d top, Vector3d bottom, uint color) {
        foreach (var side in Quarters)
            Line(lines, f.At(top + side * radius), f.At(bottom + side * radius), color);
    }

    static readonly Vector3d[] Quarters = [Vector3d.UnitX, Vector3d.UnitY, -Vector3d.UnitX, -Vector3d.UnitY];

    // Drawing

    // A circle, or a fraction of one, in the plane two local axes span. Half a turn starting on u sweeps
    // over v and back to -u, which is exactly a hemisphere seen edge-on.
    static void Arc(DataStreamGeometryProcessor lines, Frame f, Vector3d center, double radius,
        Vector3d u, Vector3d v, double turns, uint color) {
        var steps = Math.Max(3, (int)(RingSegments * turns));
        var previous = f.At(center + u * radius);

        for (var i = 1; i <= steps; i++) {
            var (sin, cos) = Math.SinCos(i / (double)steps * turns * Math.Tau);
            var next = f.At(center + (u * cos + v * sin) * radius);
            Line(lines, previous, next, color);
            previous = next;
        }
    }

    static void Cross(DataStreamGeometryProcessor lines, Vector3d at, uint color) {
        Line(lines, at - Vector3d.UnitX * CrossArm, at + Vector3d.UnitX * CrossArm, color);
        Line(lines, at - Vector3d.UnitY * CrossArm, at + Vector3d.UnitY * CrossArm, color);
        Line(lines, at - Vector3d.UnitZ * CrossArm, at + Vector3d.UnitZ * CrossArm, color);
    }

    static void Line(DataStreamGeometryProcessor lines, Vector3d from, Vector3d to, uint color) {
        var index = (uint)lines.VertexCount;
        lines.AddVertex(new DataStreamVertex(from, color));
        lines.AddVertex(new DataStreamVertex(to, color));
        lines.AddIndex(index);
        lines.AddIndex(index + 1);
    }

    // Helpers

    // A shape's own frame out in the world. Every wire above is written in plain local coordinates and
    // lands placed and turned for free.
    readonly record struct Frame(Vector3d Origin, Vector3d X, Vector3d Y, Vector3d Z) {
        public static Frame World(Vector3d origin) =>
            new(origin, Vector3d.UnitX, Vector3d.UnitY, Vector3d.UnitZ);

        public static Frame Of(Transform entity, Pose local) {
            var rotation = entity.Rotation * local.Rotation;
            return new Frame(
                entity.Position + Vector3d.Transform(new Vector3d(local.Position.X, local.Position.Y, local.Position.Z), entity.Rotation),
                Vector3d.Transform(Vector3d.UnitX, rotation),
                Vector3d.Transform(Vector3d.UnitY, rotation),
                Vector3d.Transform(Vector3d.UnitZ, rotation));
        }

        public Vector3d At(Vector3d local) => Origin + X * local.X + Y * local.Y + Z * local.Z;
    }

    // Fallback envelope, read through the support function every shape already exposes
    static (Vector3d Min, Vector3d Max) Extent(IShape shape) => (
        new Vector3d(
            shape.GetSupport(-Vector3.UnitX).X,
            shape.GetSupport(-Vector3.UnitY).Y,
            shape.GetSupport(-Vector3.UnitZ).Z),
        new Vector3d(
            shape.GetSupport(Vector3.UnitX).X,
            shape.GetSupport(Vector3.UnitY).Y,
            shape.GetSupport(Vector3.UnitZ).Z));

    static uint Rgba(byte r, byte g, byte b, byte a = 255) => (uint)(r | g << 8 | b << 16 | a << 24);
}
