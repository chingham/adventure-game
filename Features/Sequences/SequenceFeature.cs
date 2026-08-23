using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Features.Sequences;

// Scripted beats, driven by the signals the rest of the game emits.
sealed class SequenceFeature : IGameFeature {
    public void Provide(IServiceRegistry services) {
        // A service rather than a call, so what a level file may say about rules is spelled out with the
        // rest of the graph - before any file is read, whatever order the features install in.
        services.Add<SequenceVocabulary>();

        services.Add<SequenceDirector>();
    }

    public void Install(Game game) {
        game.Input.Player.SetGroup(Controls.DialogueGroup, enabled: false);

        game.AddSystem<SequenceReloadSystem>(QuarkPhases.Input, Order.SequenceSweep);
        game.AddSystem<SequenceSystem>(QuarkPhases.Gameplay, Order.Sequence);
    }
}
