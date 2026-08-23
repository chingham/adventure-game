using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Features.Progression;

// What the world remembers across a hot reload. Nothing to install: the level and the scripts read it.
sealed class ProgressionFeature : IGameFeature {
    public void Provide(IServiceRegistry services) {
        services.Add<Flags>();
    }
}
