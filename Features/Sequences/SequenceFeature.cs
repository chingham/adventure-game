using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Features.Sequences;

// Scripted beats, driven by the signals the rest of the game emits.
sealed class SequenceFeature : IGameFeature {
    public void Install(Game game) {
        game.AddSystem<SequenceSystem>(QuarkPhases.Gameplay, Order.Sequence);
    }
}
