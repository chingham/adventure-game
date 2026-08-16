using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Features.Camera;

// The cameras that watch the world: the follow rig, the doorway pull, and the director blending
// between them. Installed before the character, whose input basis is tuned against the same DoorTuning.
sealed class CameraFeature : IGameFeature {
    public void Install(Game game) {
        game.Provide<DoorTuning>();

        game.AddSystem<SpaceSystem>(QuarkPhases.Gameplay, Order.Space);
        game.AddSystem<DoorApproachSystem>(QuarkPhases.Gameplay, Order.DoorApproach);
        game.AddSystem<FollowRigSystem>(QuarkPhases.Gameplay, Order.FollowRig);
        game.AddSystem<CameraDirectorSystem>(QuarkPhases.Gameplay, Order.CameraDirector);
    }
}
