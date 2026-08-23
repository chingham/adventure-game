using Quark.Kit;
using Quark.Platform.Input;

namespace AdventureGame.Flow;

/// <summary>
/// What is on screen, and what that implies: who owns the gameplay controls and how fast the world runs.
/// Every screen change goes through here, so those two answers have exactly one author.
/// </summary>
public sealed class GameFlow(Screen startup, IInput input, GameTime time) {
    readonly List<ScreenHandle> stack = [];

    // Screen stack
    public Screen Top => stack.Count > 0 ? stack[^1].Screen : startup;
    public bool IsTop(Screen screen) => Top == screen;
    public bool IsOpen(Screen screen) => stack.Any(h => h.Screen == screen);

    public event Action<Screen>? Changed;

    public IDisposable Open(Screen screen) {
        var handle = new ScreenHandle(screen, this);
        stack.Add(handle);
        Apply();
        return handle;
    }

    public bool TryBack() {
        for (var i = stack.Count - 1; i >= 0; i--) {
            var handle = stack[i];
            if (handle.Screen.Backable) {
                handle.Dispose();
                return true;
            }
        }

        return false;
    }

    public void Reset() {
        var handles = stack.ToArray();
        foreach (var handle in handles)
            handle.Dispose();
        
        Apply();
    }

    sealed record ScreenHandle(Screen Screen, GameFlow Flow) : IDisposable {
        bool disposed;
        public void Dispose() {
            if (disposed) {
                Console.WriteLine("Warning: ScreenHandle already disposed");
                return;
            }
            disposed = true;

            if (Flow.stack.Remove(this)) {
                Flow.Apply();
            }
        }
    }

    // Apply
    readonly HashSet<string> activeGroups = [..startup.InputGroups];
    
    void Apply() {
        var top = Top;
        
        foreach (var g in activeGroups)
            if (!top.InputGroups.Contains(g))
                input.Player.SetGroup(g, false);
        
        foreach (var g in top.InputGroups)
            input.Player.SetGroup(g, true);
        
        activeGroups.Clear();
        activeGroups.UnionWith(top.InputGroups);
        
        time.Paused = top.Pause;
        time.Scale = top.TimeScale;
        input.Cursor.Mode = top.Cursor;
        
        Changed?.Invoke(top);
    }
}