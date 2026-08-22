using AdventureGame.Flow;

namespace AdventureGame.Features.Sequences;

sealed class SequenceDirector(GameFlow flow) {
    public List<SequenceRun> Runs { get; } = [];
    public bool IsRunning(string on) => Runs.Any(x => x.On == on);
    
    IDisposable? controlLock;
    public void SetPlayerLocked(bool locked) {
        if (locked && controlLock is null)
            controlLock = flow.LockPlayerControl();
        else if (!locked && controlLock is not null) {
            controlLock.Dispose();
            controlLock = null;
        }
    }
}