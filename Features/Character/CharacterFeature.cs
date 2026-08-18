using AdventureGame.App;
using AdventureGame.Level;
using Quark.Kit.Scenes.Files;
using Quark.Kit;
using Quark.Kit.Components;

namespace AdventureGame.Features.Character;

// The player: what reads the input, what moves the body, and what draws it moving.
sealed class CharacterFeature : IGameFeature {
    public void Provide(Game game) {
        game.Provide<CharacterTuning>();
    }

    public void Install(Game game) {
        var physics = game.Physics.Phase;

        // Simulation
        game.AddSystem<InputBasisSystem>(physics, Order.InputBasis);
        game.AddSystem<CharacterIntentSystem>(physics, Order.CharacterIntent);
        game.AddSystem<CharacterMovementSystem>(physics, Order.CharacterMovement);

        // Visuals, between two fixed steps
        game.AddSystem(
            new CharacterInterpolationSystem(physics), QuarkPhases.LateUpdate, Order.CharacterInterpolation);
        game.AddSystem<CharacterCapsuleAnimationSystem>(QuarkPhases.LateUpdate, Order.CharacterAnimation);

        // The rig, where the level said to put it, in the greybox the level is made of
        var spawn = game.Shared<LevelSpawn>();
        var material = game.Shared<SceneFileHandle>().Material("neutral");
        game.World.Setup(world => CharacterRig.Build(world, game, material, spawn));
    }
}
