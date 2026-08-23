using AdventureGame.App;
using AdventureGame.Flow;
using Quark.Kit;

namespace AdventureGame.Features.Sequences;

// Scripted beats, driven by the signals the rest of the game emits.
sealed class SequenceFeature : IGameFeature {
    // The level file is read during the Install pass, so its rules have to be spelled out before it
    public void Provide(Game game) {
        SequenceVocabulary.Register(game.SceneFiles.Vocabulary);

        var flow = game.Find<GameFlow>()!;
        game.Provide(new SequenceDirector(flow));
    }

    public void Install(Game game) {
        game.Input.Player.SetGroup(Controls.DialogueGroup, enabled: false);
        
        game.AddSystem<SequenceReloadSystem>(QuarkPhases.Input, Order.SequenceSweep);
        game.AddSystem<SequenceSystem>(QuarkPhases.Gameplay, Order.Sequence);
    }
}
