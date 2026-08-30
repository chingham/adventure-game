using System.Numerics;
using AdventureGame.App;
using AdventureGame.Features.Interaction;
using AdventureGame.Level;
using Quark.Ecs;
using Quark.Graphics;
using Quark.Kit;
using Quark.Kit.Assets;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Meshes;
using Quark.Kit.Rendering.Particles;
using Quark.Kit.Rendering.Particles.Behaviors;
using Quark.Numerics;
using Quark.Physics.Dimension3D;

namespace AdventureGame.Features.Character;

static class Character {
    public static Entity Spawn(
        EntityCommands world,
        DefaultRenderingModule rendering,
        GamePrimitiveLibrary primitives,
        AssetLibrary assets,
        MaterialHandle material,
        LevelSpawn spawn) {
        // Primitive
        var restPose = Pose.At(new Vector3(0, 0, CharacterShape.CapsuleRestHeight));
        var capsule = primitives.Capsule(
            CharacterShape.CapsuleRadius,
            CharacterShape.CapsuleSegmentHeight,
            material: material,
            pose: restPose);
        var box = primitives.Box(
            new Vector3(0.5f, 0.25f, 0.2f), 
            material: material);
        
        // Smoke material
        var smokeTexture = assets.LoadTexture("Data/Textures/smoke.png");
        var smokeMaterial = rendering.CreateMaterial(
            new ParticleMaterial { Response = 1, Fade = 0.8f },
            smokeTexture);
        
        // Spawn root character
        var character = world.Spawn(capsule.WithoutMesh())
            .Kinematic(layer: Layers.Physics.Character)
            .With(new InputBasis())
            .With(new CharacterIntent())
            .With(new CharacterMovement {
                Position = spawn.Point,
                PreviousPosition = spawn.Point,
                Yaw = spawn.Yaw,
                VisualYaw = spawn.Yaw,
                PreviousVisualYaw = spawn.Yaw,
            })
            .With(new CharacterAnimParams())
            .With(new Interactor())
            .With(new ParticleEmitter {
                Rate = 0f,
                Lifetime = (0.5f, 1.3f),
                Speed = (0.5f, 4f),
                RotationRate = (-0.1f, 0.1f),
                StartSize = (0.4f, 0.8f),
                StartColor = Color.Rgba(1f, 1f, 1f, 0.5f),
                StartRotation = (0f, MathF.PI * 2f),
                Emission = EmitShape.Circle(0.4f),
                Spread = (80, 95),
                Blend = BlendMode.Alpha,
                Material = smokeMaterial,
                Behaviors = [
                    new Gravity(0, 0, -0.2f),
                    new Drag { Coefficient = 6f },
                    new SizeOverLife(Curve.Keys((0f, 0.0f), (0.05f, 1.0f), (0.3f, 0.95f), (1f, 0.5f))),
                    new ColorOverLife(Gradient.Ramp((0, Vector4.One), (0.8f, Vector4.One), (1, new Vector4(1, 1, 1, 0))))
                ]
            })
            .At(spawn.Point)
            .Named("Character");
        
        // Pan mesh
        var panMesh = assets.Load<Model>("Data/Meshes/pan.glb");
        var panScale = 4.1f;
        var panOffset = new Vector3(-0.041f, -0.124f, 1.8f);
        var panRotation = Quaternion.Identity
                          * Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI)
                          * Quaternion.CreateFromAxisAngle(Vector3.UnitX, -0.4f)
                          * Quaternion.CreateFromAxisAngle(Vector3.UnitZ, 0.8f)
                          * Quaternion.CreateFromAxisAngle(Vector3.UnitY, -0.2f);
        
        // Spawn render geometry
        var renderLayers = Layers.Render.Character | LayerMask.Default;
        var body = world.Spawn(capsule, layers: renderLayers)
            .ChildOf(character)
            .Named("CharacterMesh (Body)");
        world.Spawn(box, layers: renderLayers)
            .ChildOf(body)
            .At(new Vector3(0, 0.31f, 1.3f))
            .Named("CharacterMesh (Eyes)");
        world.Spawn(panMesh, layers: renderLayers)
            .ChildOf(body)
            .Scaled(panScale).At(panOffset, panRotation)
            .Named("CharacterMesh (Pan)");
        
        world.Add(character, new CharacterCapsuleAnimation { Body = body });

        return character;
    }
}
