using AdventureGame.App;
using AdventureGame.Features.Camera;
using AdventureGame.Features.Character;
using AdventureGame.Features.Progression;
using AdventureGame.Features.Sequences;
using Quark.Kit;

namespace AdventureGame.Debug;

// Every inspector and every probe, in one place: commenting this feature out of Features leaves the
// game running with nothing drawn on top of it. The panels themselves live next to what they inspect.
sealed class DebugFeature : IGameFeature {
    public void Install(Game game) {

        // Panels
        game.AddSystem<ConsolePanel>(QuarkPhases.LateUpdate, Order.Panel);
        game.AddSystem<CharacterPanel>(QuarkPhases.LateUpdate, Order.Panel);
        game.AddSystem<CameraPanel>(QuarkPhases.LateUpdate, Order.Panel);
        game.AddSystem<ProgressionPanel>(QuarkPhases.LateUpdate, Order.Panel);

        // Collider outlines, drawn with the frame
        game.AddSystem<DebugVolumeSystem>(QuarkPhases.RenderSubmit, Order.DebugVolumes);
    }
}
