using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Ui;
using Quark.Platform.Input;

namespace AdventureGame;

public enum ScreenKind {
    Menu,
    Game,
    Inventory
}

public sealed class GameFlow(Game game, IInput input) {
    readonly Stack<ScreenKind> stack = new([ScreenKind.Game]);
    
    public ScreenKind Top => stack.Peek();
    public bool IsTop(ScreenKind kind) => Top == kind;
    public bool IsOpen(ScreenKind kind) => stack.Contains(kind);
    
    public void Push(ScreenKind kind) {
        stack.Push(kind);
        Apply();
    }
    public void Pop() {
        if (stack.Count > 1) {
            stack.Pop();
            Apply();
        }
    }
    public void ReplaceAll(ScreenKind kind) {
        stack.Clear();
        stack.Push(kind);
        Apply();
    }

    public bool TryBack() {
        if (stack.Count <= 1)
            return false;
        Pop();
        return true;
    }
    
    // Player control
    int playerControlLocks;
    
    public IDisposable LockPlayerControl() {
        playerControlLocks++;
        Apply();
        return new PlayerControl(this);
    }
    void UnlockPlayerControl() {
        if (playerControlLocks <= 0) return;
        playerControlLocks--;
        Apply();
    }

    sealed class PlayerControl(GameFlow flow) : IDisposable {
        bool disposed;
        public void Dispose() {
            if (disposed) {
                Console.WriteLine("Warning: PlayerControl already disposed");
                return;
            }
            disposed = true;
            flow.UnlockPlayerControl();
        }
    }
    
    // Apply
    void Apply() {
        input.Player.SetGroup(Controls.GameplayGroup, IsTop(ScreenKind.Game) && playerControlLocks == 0);

        game.Time.Paused = IsOpen(ScreenKind.Menu);
        game.Time.Scale = IsOpen(ScreenKind.Inventory) ? 0.5f : 1.0f;
    }
}

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