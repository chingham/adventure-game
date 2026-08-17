using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Features.Sequences;

// Scripted beats, driven by the signals the rest of the game emits.
sealed class SequenceFeature : IGameFeature {
    // The level file is read during the Install pass, so its rules have to be spelled out before it
    public void Provide(Game game) {
        SequenceVocabulary.Register(game.SceneFiles.Vocabulary);
    }

    public void Install(Game game) {
        game.AddSystem<SequenceReloadSystem>(QuarkPhases.Input, Order.SequenceSweep);
        game.AddSystem<SequenceSystem>(QuarkPhases.Gameplay, Order.Sequence);
    }
}
