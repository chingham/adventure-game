using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Kit.Scenes.Files;

namespace AdventureGame.Features.Sequences;

// What a level load owes the rules: the runners of the previous world are swept away, and every object
// a step points at is checked to still be there.
//
// A runner outlives the entities it drives, and a hot reload rebuilds them all under it - a half-played
// sequence would otherwise hold the reins of a world it no longer recognises. The flag is raised inside
// the reload and drained on the next frame, since the world is mid-callback when it fires.
sealed class SequenceReloadSystem : ISystem {
    bool pending = true;   // the first load counts too

    public SequenceReloadSystem(SceneFileHandle level) => level.Reloaded += _ => pending = true;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (!pending)
            return;
        pending = false;

        foreach (var row in world.Query<SequenceRunner>())
            commands.Destroy(row.Entity);

        ReportDanglingReferences(world);
    }

    // A step naming an object that is not there does nothing at all, silently - the worst kind of typo.
    // The check runs on the live world rather than on the file, so it also catches the object that was
    // renamed out from under a rule that still names it.
    static void ReportDanglingReferences(World world) {
        var names = new HashSet<string>();
        foreach (var row in world.Query<Name>())
            names.Add(row.Component1.Value);

        foreach (var row in world.Query<SequenceDefinition>()) {
            var definition = row.Component1;
            foreach (var step in definition.Steps)
                foreach (var reference in step.References())
                    if (!names.Contains(reference))
                        Console.Error.WriteLine(
                            $"[Level] sequence '{definition.On}': nothing here is called '{reference}'.");
        }
    }
}
