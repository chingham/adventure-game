using AdventureGame.Flow;

namespace AdventureGame.Features.Sequences;

sealed class SequenceDirector(GameFlow flow) {
    public List<SequenceRun> Runs { get; } = [];
    public bool IsRunning(string on) => Runs.Any(x => x.On == on);
}