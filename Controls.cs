using Quark.Platform.Input;

namespace AdventureGame;

static class Controls {
    // Camera
    public static readonly Delta2Action Look = new("look");
    public static readonly Delta1Action Zoom = new("zoom");
    public static readonly ButtonAction ToggleView = new("toggle_view");

    // Character
    public static readonly Axis2Action Move = new("move");
    public static readonly ButtonAction Jump = new("jump");
    public static readonly ButtonAction Interact = new("interact");

    // Cursor
    public static readonly ButtonAction GrabCursor = new("grab_cursor");
    public static readonly ButtonAction ReleaseCursor = new("release_cursor");

    // Authoring
    public static readonly ButtonAction ReloadLevel = new("reload_level");
    public static readonly ButtonAction ToggleVolumes = new("toggle_volumes");

    public static void Bind(InputMap map) {
        // Camera
        map.Delta(Look)
            .MouseMotion(sensitivity: 6.5f / 1000f)
            .Stick(PadStick.Right, deadzone: new Deadzone(0.18f, 0.95f), rate: 2.5f, invertY: true);
        map.Delta(Zoom)
            .MouseWheel()
            .Pad(PadButton.DPadDown, PadButton.DPadUp, rate: 8f);
        map.Button(ToggleView)
            .Key(InputKey.V)
            .Pad(PadButton.North);

        // Character
        map.Axis(Move)
            .Keys(up: InputKey.W, down: InputKey.S, left: InputKey.A, right: InputKey.D)
            .Stick(PadStick.Left, deadzone: new Deadzone(0.18f, 0.95f));
        map.Button(Jump)
            .Key(InputKey.Space)
            .Pad(PadButton.South);
        map.Button(Interact)
            .Key(InputKey.E)
            .Pad(PadButton.West);

        // Cursor
        map.Button(GrabCursor).Mouse(MouseButton.Left);
        map.Button(ReleaseCursor).Key(InputKey.Escape);

        // Authoring
        map.Button(ReloadLevel).Key(InputKey.F5);
        map.Button(ToggleVolumes).Key(InputKey.F3);
    }
}
