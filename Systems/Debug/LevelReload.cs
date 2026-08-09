using AdventureGame.Library;
using Quark.Ecs;
using Quark.Kit.Rendering.Meshes;
using Quark.Platform.Input;

namespace AdventureGame.Systems;

// Tags everything the brick kit spawns, so a reload knows exactly what to clear.
struct LevelBrick;

// Rebuilds the brick level when its file changes on disk, or on demand. Destroy and respawn both go
// through the command buffer, so the swap lands on a frame boundary like any structural mutation.
sealed class LevelReloadSystem : ISystem, IDisposable {
    readonly IInput input;
    readonly GamePrimitiveLibrary primitives;
    readonly GreyboxMaterials materials;
    readonly LevelFileWatcher watcher;

    public LevelReloadSystem(IInput input, GamePrimitiveLibrary primitives, GreyboxMaterials materials) {
        this.input = input;
        this.primitives = primitives;
        this.materials = materials;
        watcher = new LevelFileWatcher(LevelFile.Path);
    }

    public void Update(World world, EntityCommands commands, float deltaTime) {
        var manual = input.Button(Controls.ReloadLevel) == ButtonState.JustPressed;
        if (!watcher.ConsumePending() && !manual)
            return;

        // Read first: a level that fails to parse leaves the running one untouched.
        var result = LevelFile.TryRead(LevelFile.Path, out var level);
        if (result == LevelReadResult.Locked) {
            watcher.Rearm();   // still being written, try again next frame
            return;
        }
        if (result != LevelReadResult.Loaded)
            return;

        foreach (var row in world.Query<LevelBrick>())
            commands.Destroy(row.Entity);

        LevelFile.Apply(level, new Bricks(commands, primitives, materials), materials);
        Console.WriteLine($"[Level] Reloaded {level.Objects.Count} objects, {level.Sequences.Count} sequences.");
    }

    public void Dispose() => watcher.Dispose();
}

// Raises a pending flag when the level file changes. Polled by the system so the rebuild happens on the
// game loop, not on the watcher thread.
sealed class LevelFileWatcher : IDisposable {
    readonly FileSystemWatcher? watcher;
    volatile bool pending;

    public LevelFileWatcher(string path) {
        var full = System.IO.Path.GetFullPath(path);
        var directory = System.IO.Path.GetDirectoryName(full);
        if (directory is null || !Directory.Exists(directory))
            return;

        watcher = new FileSystemWatcher(directory, System.IO.Path.GetFileName(full)) {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };
        watcher.Changed += OnChanged;
        watcher.Created += OnChanged;
    }

    void OnChanged(object sender, FileSystemEventArgs e) => pending = true;

    public void Rearm() => pending = true;

    public bool ConsumePending() {
        if (!pending)
            return false;
        pending = false;
        return true;
    }

    public void Dispose() => watcher?.Dispose();
}
