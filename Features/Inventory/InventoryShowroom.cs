using System.Numerics;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Meshes;
using Quark.Kit.Ui;
using Quark.Numerics;
using WebGpuSharp;
using Color = Quark.Numerics.Color;
using LayerMask = Quark.Kit.LayerMask;

namespace AdventureGame.Features.Inventory;

/// <summary>
/// The inventory's items as live 3D objects: they stand in a grid in world space, one orthographic camera
/// frames the whole grid into one texture, and each slot draws its own cell of it through
/// <see cref="UiImage.Region"/>. They turn because a system turns them, not because anything re-renders.
/// <para>
/// One view for the lot, so one scene pass, one tonemap and one blit - a view per object would pay all three
/// apiece, plus its own processors and targets. The camera is <b>orthographic</b> so no cell is seen more from
/// the side than its neighbours, and the view is <b>transparent</b> so what nothing drew stays empty and the
/// objects composite onto the panel instead of arriving in a black box.
/// </para>
/// </summary>
sealed record InventoryShowroom(UiImage Atlas) {
    // One cell per item, as atlas pixels and as world units. Both describe the same grid, which is what makes
    // a cell's region and a cell's world centre the same arithmetic.
    const float Cell = 96f;
    const double CellSize = 2.2;
    const int Columns = 2;
    const int Rows = 2;

    // Its own visibility layer: the studio's objects and lights never reach the main view, and the world never
    // reaches the atlas.
    static readonly LayerMask Layer = LayerMask.FromBit(31);

    public static InventoryShowroom Build(Game game) {
        var rendering = game.Rendering;
        var atlas = rendering.Textures.CreateRenderTarget(
            "Showroom", (uint)(Cell * Columns), (uint)(Cell * Rows), TextureFormat.RGBA8Unorm);

        /*var view = rendering.AddView(atlas);
        view.Layers = Layer;
        view.Transparent = true;
        view.RendersShadows = false;

        // The main view renders every layer by default, which includes this one - the studio has to be taken
        // out of it explicitly or the objects would also float in the middle of the scene.
        rendering.MainView.Layers = rendering.MainView.Layers.Without(Layer);

        var items = Items(game);
        game.World.Setup(world => {
            for (var i = 0; i < items.Length; i++)
                world.Spawn(items[i])
                    .At(CellCenter(i))
                    .With(new Spin { Speed = 0.5 + i * 0.12 });

            // Studio lighting: a key light and a cooler fill, both on the studio's layer so the scene is lit
            // by its own sun and this grid by these two.
            world.Spawn(Light.Directional(
                Vector3.Normalize(new Vector3(-0.5f, 0.6f, -0.5f)), Vector3.One, 2.4f) with { Layers = Layer });
            world.Spawn(Light.Directional(
                Vector3.Normalize(new Vector3(0.7f, 0.4f, 0.2f)),
                new Vector3(0.55f, 0.65f, 0.9f), 1.1f) with { Layers = Layer });

            // Ortho, so the grid's own height is what the camera covers and the width follows the aspect the
            // atlas already has. Straight down -Y, since the grid is laid out in the XZ plane.
            var eye = new Vector3d(0, -20, 0);
            world.Spawn(new CameraComponent {
                    OrthographicHeight = Rows * CellSize,
                    NearPlane = 1,
                    FarPlane = 40,
                    View = view.Handle
                })
                .At(eye, Rotations.LookAt(eye, Vector3d.Zero));
        });

        game.AddSystem<SpinSystem>(QuarkPhases.LateUpdate, order: 5);*/
        return new InventoryShowroom(game.Ui.Image(atlas));
    }

    /// <summary>The cell one item was rendered into - an image like any other, and all of them one batch.</summary>
    public UiImage Slot(int index) =>
        Atlas.Region(index % Columns * Cell, index / Columns * Cell, Cell, Cell);

    // Where an item stands: the same row-major walk the atlas cells are in, centred on the origin so the
    // camera needs no offset of its own.
    static Vector3d CellCenter(int index) => new(
        (index % Columns - (Columns - 1) * 0.5) * CellSize,
        0,
        ((Rows - 1) * 0.5 - index / Columns) * CellSize);

    // Four shapes with their own materials, so the grid exercises several meshes rather than one repeated.
    static RenderMesh[] Items(Game game) {
        var rendering = game.Rendering;
        var primitives = game.Primitives;

        return [
            Item(primitives.Icosphere(0.75f, 2, Metal(rendering, Color.Rgb(1.0f, 0.45f, 0.35f), 0.35f))),
            Item(primitives.Box(new Vector3(1.2f), Metal(rendering, Color.Rgb(0.55f, 0.80f, 1.0f), 0.20f))),
            Item(primitives.Pyramid(1.4f, 1.5f, Metal(rendering, Color.Rgb(0.65f, 1.0f, 0.60f), 0.45f))),
            Item(primitives.Cylinder(0.35f, 1.5f, Metal(rendering, Color.Rgb(1.0f, 0.85f, 0.45f), 0.15f), sides: 16))
        ];
    }

    // Spawned as a plain render mesh rather than through Spawn(GameMesh), which carries no layer - and the
    // layer is the whole point of a studio the main view must not see.
    static RenderMesh Item(GameMesh mesh) =>
        new() { Mesh = mesh.Mesh!, Material = mesh.Material, Layers = Layer };

    static MaterialHandle Metal(DefaultRenderingModule rendering, Color color, float roughness) =>
        rendering.CreateMaterial(new PbrMaterial { BaseColor = color, Roughness = roughness, Metallic = 0.7f });
}

struct Spin {
    public double Speed;
    public double Time;
}

sealed class SpinSystem : ISystem {
    readonly Query<RelativeTransform, Spin> query = new(Turn);

    public void Update(World world, EntityCommands commands, float deltaTime) =>
        query.Execute(world, commands, deltaTime);

    static void Turn(
        World world, EntityCommands commands, float deltaTime,
        Entity entity, ref RelativeTransform transform, ref Spin spin) {
        spin.Time += deltaTime;
        transform.LocalTransform = transform.LocalTransform with {
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, (float)(spin.Time * spin.Speed))
        };
    }
}
