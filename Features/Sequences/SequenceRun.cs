using Quark.Ecs;
using Quark.Kit.Components;
using IServiceProvider = Quark.Kit.IServiceProvider;

namespace AdventureGame.Features.Sequences;

/// <summary>
/// A single run of a sequence, which is a list of steps.
/// </summary>
sealed class SequenceRun(string on, Entity source, IStep[] steps, World world, IServiceProvider services) {
    public string On { get; } = on;
    public Entity Source { get; } = source;
    public StepList Steps { get; } = new(steps);
    public int Gates { get; set; }
    
    public World World { get; } = world;

    public T Get<T>() where T : class => services.Get<T>();

    public List<Signal> Heard { get; } = [];

    public Entity Find(string id) {
        foreach (var row in World.Query<Name>())
            if (row.Component1.Value == id)
                return row.Entity;
        return Entity.Null;
    }
}

sealed class StepList(IStep[] steps) : IDisposable {
    int next;
    IStepRun? active;

    public bool Done => active is null && next >= steps.Length;

    public bool Advance(SequenceRun run, float deltaTime) {
        while (true) {
            if (active is { } c) {
                if (!c.Update(run, deltaTime)) return false;
                c.Dispose();
                active = null;
            }
            
            if (next >= steps.Length) return true;
            active = steps[next++].Start(run);
        }
    }

    public void Dispose() {
        active?.Dispose();
        active = null;
    }
}