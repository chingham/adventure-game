using AdventureGame.App;
using Quark.Kit;
using Quark.Platform.Input;

namespace AdventureGame.Flow;

public enum ScreenKind {
    Menu,
    Game,
    Inventory
}

/// <summary>
/// What is on screen, and what that implies: who owns the gameplay controls and how fast the world runs.
/// Every screen change goes through here, so those two answers have exactly one author.
/// </summary>
public sealed class GameFlow(Game game, IInput input) {
    readonly Stack<ScreenKind> stack = new([ScreenKind.Game]);

    // Screen stack
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
    //
    // Refcounted: a cutscene, a dialogue and an open menu can hold the reins at the same time without
    // taking them back from each other.
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
