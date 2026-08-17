using AdventureGame.App;
using Quark.Kit;

namespace AdventureGame.Features.Camera;

// The cameras that watch the world: the follow rig, the doorway pull, and the director blending
// between them.
sealed class CameraFeature : IGameFeature {
    public void Provide(Game game) {
        game.Provide<CameraTuning>();
    }

    public void Install(Game game) {
        // Zones first: what follows frames and fogs whatever they resolved to
        game.AddSystem<CameraZoneSystem>(QuarkPhases.Gameplay, Order.Zones);
        game.AddSystem<FogZoneSystem>(QuarkPhases.Gameplay, Order.Zones);
        game.AddSystem<GradeZoneSystem>(QuarkPhases.Gameplay, Order.Zones);
        game.AddSystem<DoorApproachSystem>(QuarkPhases.Gameplay, Order.DoorApproach);
        game.AddSystem<FollowRigSystem>(QuarkPhases.Gameplay, Order.FollowRig);
        game.AddSystem<CameraDirectorSystem>(QuarkPhases.Gameplay, Order.CameraDirector);
    }
}
