using System.Numerics;
using AdventureGame.App;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Kit.Profiling;
using Quark.Kit.Ui;

Console.WriteLine("F3: Show hidden volumes   F5: Reload level");

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
    .Setup(Features.InstallAll)
    .Run();
