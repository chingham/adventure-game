using AdventureGame.App;
using Quark.Ecs;
using Quark.Kit.Ui;
using Quark.Platform.Input;

namespace AdventureGame.Flow;

// Turns the shell buttons into screen changes. Cancel backs out of whatever is on top first, so a
// screen never has to know what opened it.
sealed class FlowSystem(IInput input, GameFlow flow) : ISystem {
    IDisposable? menu, inventory;
    
    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (input.Button(UiControls.Cancel) == ButtonState.JustPressed && flow.TryBack()) 
            return;

        if (input.Button(Controls.Pause) == ButtonState.JustPressed) {
            Toggle(ref menu, Screens.Menu);
        }

        if (input.Button(Controls.OpenInventory) == ButtonState.JustPressed) {
            Toggle(ref inventory, Screens.Inventory);
        }
    }
    
    void Toggle(ref IDisposable? handle, Screen screen) {
        if (handle is null) {
            if (flow.IsTop(Screens.Game))
                handle = flow.Open(screen);
        }
        else {
            handle.Dispose();
            handle = null;
        }
    }
}
