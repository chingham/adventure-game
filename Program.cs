using System.Numerics;
using AdventureGame.App;
using Quark.Kit;
using Quark.Kit.Capture;
using Quark.Kit.Components;
using Quark.Kit.Profiling;
using Quark.Kit.Rendering.AmbientOcclusion;
using Quark.Kit.Ui;

Console.WriteLine("F3: Show hidden volumes   F5: Reload level");

Game.Create("Adventure Game", 1920, 1080, vsync: false)
    .UseInput(Controls.Bind)
    .UseDefaultRendering(rendering => {
        rendering.Stencil = true;
        rendering.DepthPrepass = true;

        rendering.AmbientOcclusion.Enabled = true;
        rendering.AmbientOcclusion.Quality = AmbientOcclusionQuality.Balanced;
        rendering.AmbientOcclusion.Filter  = AmbientOcclusionFilter.High;
        rendering.AmbientOcclusion.Radius = 2.0f;
        rendering.AmbientOcclusion.Intensity = 0.8f;
        rendering.AmbientOcclusion.DirectOcclusion = 0.3f;
        rendering.AmbientOcclusion.Power = 2f;
        //rendering.AmbientOcclusion.Debug = AmbientOcclusionDebug.Occlusion;
    })
    .UsePhysics(physics => {
        physics.Gravity = new Vector3(0, 0, -9.81f);
        physics.MaxTicksPerUpdate = 8;
    })
    .UseProfiling()
    .UseAssets()
    .UseSceneFiles()
    .UseUi()
    .UseCapture(capture => {
        capture.Name = "adventure-game";
        capture.Directory = "/Users/thomas/Desktop/Gamedev/Adventure Game/Captures";
    })
    .Setup(Features.InstallAll)
    .Run();
