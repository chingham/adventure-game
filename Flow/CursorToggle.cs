using AdventureGame.App;
using Quark.Ecs;
using Quark.Platform.Input;

namespace AdventureGame.Flow;

sealed class CursorToggleSystem(IInput input) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {
        // A click the UI ate never reaches the action, so there is nothing to test here
        if (input.Cursor.Mode != CursorMode.Locked && input.Button(Controls.GrabCursor) == ButtonState.JustPressed)
            input.Cursor.Mode = CursorMode.Locked;

        if (input.Button(Controls.ReleaseCursor) == ButtonState.JustPressed)
            input.Cursor.Mode = CursorMode.Visible;
    }
}
