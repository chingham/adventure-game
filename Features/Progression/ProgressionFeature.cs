using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Features.Progression;

// What the world remembers across a hot reload. Provided early: the level and the scripts both read it.
sealed class ProgressionFeature : IGameFeature {
    public void Install(Game game) {
        game.Provide<Flags>();
    }
}
