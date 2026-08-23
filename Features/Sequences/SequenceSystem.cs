using Quark.Ecs;
using Quark.Kit;

namespace AdventureGame.Features.Sequences;

sealed class SequenceSystem(Game game, SequenceDirector director) : ISystem {
    readonly EventReader<Signal> signals = new();
    readonly List<Signal> heard = [];

    public void Update(World world, EntityCommands commands, float deltaTime) {
        heard.Clear();
        
        // For each signal
        foreach (var signal in signals.Read(world)) {
            
            // Add to heard list so steps can check it
            heard.Add(signal);
            
            // Start new run if we have a sequence associated with this signal (and if not already running)
            foreach (var row in world.Query<SequenceDefinition>()) {
                var definition = row.Component1;
                if (definition.On != signal.Name || director.IsRunning(definition.On))
                    continue;

                StartRun(world, definition, signal);
            }
        }

        UpdateRuns(world, deltaTime);
    }

    void StartRun(World world, SequenceDefinition definition, Signal signal) {
        var run = new SequenceRun(definition.On, signal.Source, definition.Steps, world, game.Services);
        director.Runs.Add(run);
    }

    void UpdateRuns(World world, float deltaTime) {
        for (var i = 0; i < director.Runs.Count; i++) {
            var run = director.Runs[i];
            
            // Update heard signals for this run
            run.Heard.Clear();
            run.Heard.AddRange(heard);

            if (!run.Steps.Advance(run, deltaTime)) continue;
            
            world.Events<Signal>().Write(new Signal(run.On + ".done", run.Source));
            director.Runs.RemoveAt(i--);
        }
    }
}