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
        UpdatePlayerLock();
    }

    void StartRun(World world, SequenceDefinition definition, Signal signal) {
        var run = new SequenceRun(definition.On, signal.Source, world, game.Services);
        foreach (var step in definition.Steps)
            run.Pending.Add(step);
                
        director.Runs.Add(run);
    }

    void UpdateRuns(World world, float deltaTime) {
        for (var i = 0; i < director.Runs.Count; i++) {
            var run = director.Runs[i];
            
            // Update heard signals for this run
            run.Heard.Clear();
            run.Heard.AddRange(heard);

            while (true) {
                // If there is a current run, update it
                if (run.Active is { } active) {
                    
                    // Update the current step, and break if it is not yet complete
                    var completed = active.Update(run, deltaTime);
                    if (!completed)  break;

                    // Clear it and remove the step from pending
                    run.Active = null;
                }

                // If no more pending step
                if (run.Pending.Count == 0) {
                    // Remove the run and emit .done signal
                    world.Events<Signal>().Write(new Signal(run.On + ".done", run.Source));
                    director.Runs.RemoveAt(i--);
                    break;
                }

                // Start the next step if any
                var step = run.Pending[0];
                run.Pending.RemoveAt(0);
                run.Active = step.Start(run);
            }
        }
    }
    void UpdatePlayerLock() {
        var locked = false;
        foreach (var run in director.Runs)
            locked |= run.Gates > 0;
        
        director.SetPlayerLocked(locked);
    }
}