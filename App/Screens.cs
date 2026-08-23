using AdventureGame.Flow;
using Quark.Kit.Ui;
using Quark.Platform.Input;

namespace AdventureGame.App;

public static class Screens {
    public static readonly Screen Game = new("game") {
        InputGroups = [Controls.GameplayGroup, Controls.FlowGroup]
    };
    public static readonly Screen Menu = new("menu") {
        InputGroups = [UiControls.Group, Controls.FlowGroup], 
        Pause = true, 
        Cursor = CursorMode.Visible, 
        Backable = true
    };
    public static readonly Screen Inventory = new("inventory") { 
        InputGroups = [UiControls.Group], 
        Pause = true, 
        Cursor = CursorMode.Visible, 
        Backable = true 
    };
    public static readonly Screen Dialogue = new("dialogue") { 
        InputGroups = [Controls.DialogueGroup]
    };
    public static readonly Screen Cutscene = new("cutscene") { 
        InputGroups = [] // TODO: Cutscene group, for actions to skip it or pause during it, etc.
    };
}