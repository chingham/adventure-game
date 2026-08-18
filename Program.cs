using System.Numerics;
using AdventureGame.App;
using Quark.Kit;
using Quark.Kit.Capture;
using Quark.Kit.Components;
using Quark.Kit.Profiling;
using Quark.Kit.Ui;
using Quark.Platform.Input;

Console.WriteLine("F3: Show hidden volumes   F5: Reload level");

Game.Create("Adventure Game", 2560, 1440, vsync: true)
    .UseInput(Controls.Bind)
    .UseDefaultRendering(rendering => {
        rendering.Stencil = true;
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
        capture.Directory = "~/Desktop/game-capture";
        capture.Hotkey = InputKey.Insert;
    })
    .Setup(Features.InstallAll)
    .Run();
