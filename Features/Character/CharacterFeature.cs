using AdventureGame.App;
using AdventureGame.Level;
using Quark.Kit;
using Quark.Kit.Components;

namespace AdventureGame.Features.Character;

// The player: what reads the input, what moves the body, and what draws it moving.
sealed class CharacterFeature : IGameFeature {
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

        // The rig, where the level said to put it
        var spawn = game.Shared<LevelSpawn>().Point;
        var materials = game.Shared<GreyboxMaterials>();
        game.World.Setup(world => CharacterRig.Build(world, game, materials, spawn));
    }
}
