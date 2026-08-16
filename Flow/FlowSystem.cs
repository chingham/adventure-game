using AdventureGame.App;
using Quark.Ecs;
using Quark.Kit.Ui;
using Quark.Platform.Input;

namespace AdventureGame.Flow;

// Turns the shell buttons into screen changes. Cancel backs out of whatever is on top first, so a
// screen never has to know what opened it.
sealed class FlowSystem(IInput input, GameFlow flow) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (input.Button(UiControls.Cancel) == ButtonState.JustPressed) {
            if (flow.TryBack()) return;
        }

        if (input.Button(Controls.Pause) == ButtonState.JustPressed) {
            switch (flow.Top) {
                case ScreenKind.Game:
                    flow.Push(ScreenKind.Menu);
                    break;

                case ScreenKind.Menu:
                    flow.Pop();
                    break;
            }
        }

        if (input.Button(Controls.OpenInventory) == ButtonState.JustPressed) {
            switch (flow.Top) {
                case ScreenKind.Game:
                    flow.Push(ScreenKind.Inventory);
                    break;

                case ScreenKind.Inventory:
                    flow.Pop();
                    break;
            }
        }
    }
}
