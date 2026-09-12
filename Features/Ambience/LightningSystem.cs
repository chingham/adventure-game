using System.Numerics;
using AdventureGame.Features.Camera;
using AdventureGame.Level;
using Quark.Ecs;
using Quark.Graphics;
using Quark.Kit.Assets;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Features.Ambience;

struct Lightning {
    public FloatRange Frequency;
    public MaterialHandle<LightningMaterial> Material;
    public Entity Halo;
    
    internal float Timer;
    internal float Clock;
}

// The sphere of lit air around a strike, moved onto each bolt and driven with it.
struct LightningHalo {
    public MaterialHandle<LightningHaloMaterial> Material;
}

sealed class LightningSystem(Context context, DefaultRenderingModule rendering, Ambience ambience, IInput input) : ISystem {
    const float StrikeDuration = 0.18f;
    const float StrikeIntensity = 7f;
    const float StrikeAmbient = 6f;
    const float StrikeFog = 1.6f;
    const float HaloIntensity = 50f;
    public const float HaloRadius = 250f;

    bool wasPressed;
    
    public void Update(World world, EntityCommands commands, float deltaTime) {
        var triggerPressed = input.Player.Devices.Keyboard?.IsPressed(InputKey.L) == true;
        var trigger = triggerPressed && !wasPressed;
        wasPressed = triggerPressed;
        
        foreach (var e in world.Query<Lightning, RelativeTransform, Light>()) {
            ref var lightning = ref e.Component1;
            ref var transform = ref e.Component2;
            ref var light = ref e.Component3;
            
            // Get zone
            if (!CameraListener.TryResolve(world, out var listener, out _))
                return;
            
            var zone = Zones.Resolve<AmbienceZone>(world, listener)?.Aspect ?? Zones.Authored<AmbienceZone>(world);
            
            // Update strikes
            Update(world, ref lightning, ref light, zone, deltaTime);
            
            // Check if timer has elapsed
            lightning.Timer -= deltaTime;
            if (/*lightning.Timer <= 0 ||*/ trigger) {
                lightning.Timer += lightning.Frequency.Lerp(Random.Shared.NextSingle());
                lightning.Clock = 0;
                
                // Trigger a lightning strike
                var position = Vector3d.Transform(Vector3d.Zero, transform.GlobalMatrix);
                Strike(world, e.Entity, ref lightning, position);
            }
        }
    }

    void Strike(World world, Entity entity, ref Lightning lightning, Vector3d position) {
        const float OffsetLength = 100;
        const float Radius = 0.3f;
        const int CylinderSteps = 5;
        
        // Get mesh
        if (!world.Has<RenderMesh>(entity)) return;
        ref var renderMesh = ref world.Get<RenderMesh>(entity);
        
        if (renderMesh.Mesh is not ListMesh<MeshVertex> mesh) return;
        
        // Generate paths
        var offsetX = (Random.Shared.NextSingle() - 0.5f) * OffsetLength;
        var offsetY = (Random.Shared.NextSingle() - 0.5f) * OffsetLength;

        var skyPosition = position + new Vector3d(offsetX, offsetY, 200);
        var paths = BoltPath.Generate(skyPosition, position);

        // Centre the halo halfway up the bolt
        if (world.Has<RelativeTransform>(lightning.Halo))
            world.Get<RelativeTransform>(lightning.Halo).LocalTransform.Position = (position + skyPosition) * 0.5;
        
        // Generate vertices + indices
        var vertices = new List<MeshVertex>();
        var indices = new List<uint>();

        foreach (var path in paths) {
            AppendTube(path.Points, Vector3d.Zero, Vector3.One);
        }
        
        // Update dynamic mesh
        mesh.Clear();
        foreach (var v in vertices) mesh.AddVertex(v);
        foreach (var i in indices) mesh.AddIndex(i);

        var gpu = context.Resources;
        mesh.Flush(gpu.Device.GetQueue());

        return;
        
        void AppendTube(ReadOnlySpan<BoltPoint> branch, Vector3d origin, Vector3 color) {
            if (branch.Length < 2)
                return;

            var first = (uint)vertices.Count;
            var up = Across(Heading(branch, 0));

            for (var i = 0; i < branch.Length; i++) {
                var forward = Heading(branch, i);
                up = Vector3.Normalize(up - forward * Vector3.Dot(up, forward));
                var right = Vector3.Cross(forward, up);

                var center = (Vector3)(branch[i].Position - origin);
                var radius = Radius * branch[i].Intensity;

                for (var s = 0; s < CylinderSteps; s++) {
                    var angle = s / (float)CylinderSteps * MathF.Tau;
                    var normal = right * MathF.Cos(angle) + up * MathF.Sin(angle);

                    vertices.Add(new MeshVertex {
                        Position = center + normal * radius,
                        Normal = normal,
                        Uv = new Vector2(s / (float)CylinderSteps, i / (float)(branch.Length - 1)),
                        Color = new Vector4(color * branch[i].Intensity, 1f)
                    });
                }
            }

            for (var i = 0; i < branch.Length - 1; i++) {
                for (var s = 0; s < CylinderSteps; s++) {
                    var a = first + (uint)(i * CylinderSteps + s);
                    var b = first + (uint)(i * CylinderSteps + (s + 1) % CylinderSteps);
                    var c = a + CylinderSteps;
                    var d = b + CylinderSteps;

                    indices.Add(a); indices.Add(c); indices.Add(b);
                    indices.Add(b); indices.Add(c); indices.Add(d);
                }
            }
        }
        static Vector3 Heading(ReadOnlySpan<BoltPoint> branch, int i) {
            var from = branch[i > 0 ? i - 1 : i].Position;
            var to = branch[i < branch.Length - 1 ? i + 1 : i].Position;
            var span = (Vector3)(to - from);
            return span.LengthSquared() > 1e-8f ? Vector3.Normalize(span) : Vector3.UnitZ;
        }
        static Vector3 Across(Vector3 forward) {
            var reference = MathF.Abs(forward.Z) < 0.99f ? Vector3.UnitZ : Vector3.UnitX;
            return Vector3.Normalize(Vector3.Cross(reference, forward));
        }
    }
    void Update(World world, ref Lightning lightning, ref Light light, AmbienceZone? zone, float deltaTime) {
        
        // Compute lightning clock
        lightning.Clock += deltaTime;
        
        var t = lightning.Clock / StrikeDuration;
        if (t > 1) t = 1;

        // Compute intensity
        var intensity = Math.Max(0, (1 - t) * (MathF.Sin(t * MathF.PI * 3 + MathF.PI / 2) * 0.4f + 0.6f));
        
        // Assign intensity to light, env, fog and material
        light.Intensity = StrikeIntensity * intensity;

        if (rendering.MainView.LightingEnvironment is { } env) {
            env.Intensity = StrikeAmbient * intensity + 1;
        }

        if (ambience.Fog.Enabled && zone?.Fog?.Color is { } fogColor) {
            ambience.Fog.Value = ambience.Fog.Value with {
                Color = Vector4.Lerp(fogColor, fogColor * StrikeFog, intensity)
            };
        }

        var mat = lightning.Material;
        mat.Value = mat.Value with { Fade = intensity };

        if (world.Has<LightningHalo>(lightning.Halo)) {
            var halo = world.Get<LightningHalo>(lightning.Halo).Material;
            halo.Value = halo.Value with { Intensity = HaloIntensity * intensity };
        }
    }
}