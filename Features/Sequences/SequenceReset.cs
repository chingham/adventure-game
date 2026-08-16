using Quark.Ecs;
using Quark.Kit.Scenes.Files;

namespace AdventureGame.Features.Sequences;

// A runner outlives the entities it drives, and a hot reload rebuilds them all under it. Sweeping the
// runners away on reload is what keeps a half-played sequence from holding the reins of a world it no
// longer recognises. The flag is raised inside the reload and drained on the next frame, since the
// world is already mid-callback when it fires.
sealed class SequenceResetSystem : ISystem {
    bool pending;

    public SequenceResetSystem(SceneFileHandle level) => level.Reloaded += _ => pending = true;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (!pending)
            return;
        pending = false;

        foreach (var row in world.Query<SequenceRunner>())
            commands.Destroy(row.Entity);
    }
}
