using System.Numerics;
using Quark.Assets;
using AdventureGame;
using AdventureGame.Library;
using AdventureGame.Systems;
using AdventureGame.Systems.Camera;
using AdventureGame.Systems.CharacterController;
using Quark.Kit;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Environments;
using Quark.Kit.Rendering.Meshes;
using Quark.Kit.Rendering.PostEffects;
using Quark.Numerics;

/* TODO LIST
 - Fixed tick rate
 - Trail en debug line derrière le perso
 - Interpolation
 - Radial deadzone (inputs)
 */

Game.Create("Third Person - Quark", 1920, 1080, vsync: true)
    .UseInput(Controls.Bind)
    .UseDefaultRendering(rendering => {
        //rendering.MsaaSampleCount = 1;
        //rendering.Lighting.EnableShadows = false;
    })
    .UsePhysics(physics => {
        physics.Gravity = new Vector3(0, 0, -9.81f);
        physics.MaxTicksPerUpdate = 8;
    })
    .UseProfiling()
    .UseAssets()
    .Setup(game => {
        // Tunables the door transition is dialled in with, live
        game.Provide(new DoorTuning());

        // Rendering
        game.Rendering.AddEffect(new Bloom { Threshold = 1.2f, Radius = 1.3f, Intensity = 0.22f });

        // Create assets
        var greyboxUv = UvMapping.Tiled(GreyboxMaterials.TileMeters);
        game.Primitives.DefaultUv = greyboxUv;
        var materials = new GreyboxMaterials(game.Rendering, game.Assets);
        var shadowMap = game.Rendering.CreateCascadeMap(new ShadowSettings());
        var terrain = game.Meshes.From(
            GreyboxMeshes.Terrain(16, 8, 24, 12, 0.5f, greyboxUv), MeshCollider.Mesh, materials.Danger);
        
        // Environment
        var environment = game.Rendering.CreateEnvironment(new GradientSky {
            Horizon = Color.FromHex("6E6493"),
            Zenith = Color.FromHex("1B2440"),
            Ground = Color.FromHex("0E0F15")
        });
        game.Rendering.SetEnvironment(environment.Value);
        
        // Post process
        var fog = game.Rendering.AddPostEffect(new DepthFogEffect { Color = Color.FromHex("0E0F15"), Density = 0.02f });
        game.Rendering.AddPostEffect(new FilmEffect { Vignette = 0.3f });
        game.Rendering.AddPostEffect(new GradeEffect { Saturation = 0.9f, Warmth = 0.1f });

        // Setup world
        game.World.Setup(world => {
            // Build playground environment (swap with BrickPlayground / Playground for the coded ones)
            LevelPlayground.Build(world, game.Primitives, materials, shadowMap, terrain);
            
            // Spawn character
            CharacterRig.Build(world, game, materials);
        });

        // Systems
        game.AddSystem<CursorToggleSystem>(QuarkPhases.Input, order: 10);
        game.AddSystem(
            game.ToDispose(new LevelReloadSystem(game.Input, game.Primitives, materials)),
            QuarkPhases.Input, order: 20);
        
        game.AddSystem<MovingPlatformSystem>(game.PhysicsPhase, order: 5);
        game.AddSystem<InputBasisSystem>(game.PhysicsPhase, order: 10);
        game.AddSystem<CharacterIntentSystem>(game.PhysicsPhase, order: 110);
        game.AddSystem<CharacterMovementSystem>(game.PhysicsPhase, order: 120);
        
        game.AddSystem<InteractionSystem>(QuarkPhases.Gameplay, order: -13);
        game.AddSystem<TriggerProbeSystem>(QuarkPhases.Gameplay, order: -12);
        game.AddSystem<InteractionProbeSystem>(QuarkPhases.Gameplay, order: -12);
        game.AddSystem<ButtonSystem>(QuarkPhases.Gameplay, order: -11);
        game.AddSystem<PlatformCallSystem>(QuarkPhases.Gameplay, order: -10);
        game.AddSystem<PortalSystem>(QuarkPhases.Gameplay, order: -10);
        game.AddSystem(new SpaceSystem(fog), QuarkPhases.Gameplay, order: -7);
        game.AddSystem<DoorApproachSystem>(QuarkPhases.Gameplay, order: -6);
        game.AddSystem<FollowRigSystem>(QuarkPhases.Gameplay, order: -5);
        game.AddSystem<CameraDirectorSystem>(QuarkPhases.Gameplay, order: -4);
        
        game.AddSystem(new CharacterInterpolationSystem(game.PhysicsPhase), QuarkPhases.LateUpdate, order: -10);
        game.AddSystem<CharacterCapsuleAnimationSystem>(QuarkPhases.LateUpdate, order: -8);

        game.AddSystem<CharacterDebugPanel>(QuarkPhases.LateUpdate);
        game.AddSystem<DoorTuningPanel>(QuarkPhases.LateUpdate);

        // Help message
        Console.WriteLine("Click the scene to grab the cursor, Esc to release it");
        Console.WriteLine("Mouse: look   WASD: move   Wheel: zoom   V: Toggle camera   E: Interact");
    })
    .Run();
