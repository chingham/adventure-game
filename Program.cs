using System.Numerics;
using Quark.Assets;
using AdventureGame;
using AdventureGame.Inventory;
using AdventureGame.Library;
using AdventureGame.Systems;
using AdventureGame.Systems.Camera;
using AdventureGame.Systems.CharacterController;
using AdventureGame.Ui;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Kit.Profiling;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Environments;
using Quark.Kit.Rendering.Meshes;
using Quark.Kit.Rendering.PostEffects;
using Quark.Kit.Ui;
using Quark.Numerics;

Game.Create("Adventure Game", 1920, 1080, vsync: true)
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
    .UseUi()
    .Setup(game => {
        
        // Flow
        var flow = game.Provide(new GameFlow(game, game.Input));
        flow.ReplaceAll(ScreenKind.Game);
        flow.Push(ScreenKind.Inventory);
        
        game.AddSystem<FlowSystem>(QuarkPhases.Input);
        
        // Tunables the door transition is dialled in with, live
        game.Provide(new DoorTuning());

        // What the world remembers across a hot reload, who holds the reins, and what it says out loud
        game.Provide<Flags>();
        game.Provide<Toasts>();

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
            // Build the level, which says where the player starts
            var spawn = LevelPlayground.Build(world, game.Primitives, materials, shadowMap, terrain);

            // Spawn character
            CharacterRig.Build(world, game, materials, spawn);
        });
        
        // UI
        var fonts = Fonts.Build(game);
        var icons = Icons.Build(game);
        var showroom = InventoryShowroom.Build(game);
        var inventory = new Inventory();
        
        game.Ui.Add(new InventoryPanel(game.Ui, flow, fonts, icons, inventory, showroom));
        game.Ui.Add(new MenuPanel(game.Ui, flow, fonts));

        // Systems
        game.AddSystem<CursorToggleSystem>(QuarkPhases.Input, order: 10);
        game.AddSystem(
            game.ToDispose(new LevelReloadSystem(game.Input, game.Primitives, materials)),
            QuarkPhases.Input, order: 20);
        
        game.AddSystem<MovingPlatformSystem>(game.Physics.Phase, order: 5);
        game.AddSystem<InputBasisSystem>(game.Physics.Phase, order: 10);
        game.AddSystem<CharacterIntentSystem>(game.Physics.Phase, order: 110);
        game.AddSystem<CharacterMovementSystem>(game.Physics.Phase, order: 120);
        
        game.AddSystem<InteractionSystem>(QuarkPhases.Gameplay, order: -20);
        game.AddSystem<TriggerSystem>(QuarkPhases.Gameplay, order: -19);
        game.AddSystem<SequenceSystem>(QuarkPhases.Gameplay, order: -18);
        game.AddSystem<TriggerProbeSystem>(QuarkPhases.Gameplay, order: -17);
        game.AddSystem<InteractionProbeSystem>(QuarkPhases.Gameplay, order: -17);
        game.AddSystem<PlatformCallSystem>(QuarkPhases.Gameplay, order: -10);
        game.AddSystem<PortalSystem>(QuarkPhases.Gameplay, order: -10);
        game.AddSystem(new SpaceSystem(fog), QuarkPhases.Gameplay, order: -7);
        game.AddSystem<DoorApproachSystem>(QuarkPhases.Gameplay, order: -6);
        game.AddSystem<FollowRigSystem>(QuarkPhases.Gameplay, order: -5);
        game.AddSystem<CameraDirectorSystem>(QuarkPhases.Gameplay, order: -4);
        
        game.AddSystem(new CharacterInterpolationSystem(game.Physics.Phase), QuarkPhases.LateUpdate, order: -10);
        game.AddSystem<CharacterCapsuleAnimationSystem>(QuarkPhases.LateUpdate, order: -8);

        game.AddSystem<CharacterDebugPanel>(QuarkPhases.LateUpdate);
        game.AddSystem<DoorTuningPanel>(QuarkPhases.LateUpdate);
        game.AddSystem<FlagsPanel>(QuarkPhases.LateUpdate);
        game.AddSystem<ToastPanel>(QuarkPhases.LateUpdate);

        game.AddSystem<DebugVolumeSystem>(QuarkPhases.RenderSubmit, order: 10);

        // Help message
        Console.WriteLine("Click the scene to grab the cursor, Esc to release it");
        Console.WriteLine("Mouse: look   WASD: move   Wheel: zoom   V: Toggle camera   E: Interact");
        Console.WriteLine("F3: Show hidden volumes   F5: Reload level");
    })
    .Run();
