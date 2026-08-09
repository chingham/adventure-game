using AdventureGame.Library;
using AdventureGame.Systems.Camera;
using AdventureGame.Systems.CharacterController;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame;

static class CharacterRig {
    public static void Build(EntityCommands entityCommands, Game game, GreyboxMaterials greyboxMaterials, Vector3d spawn) {
        var character = Character.Spawn(entityCommands, game.Rendering, game.Primitives, game.Assets, greyboxMaterials, spawn);
            
        // Spawn rigs and camera
        var followRig = FollowRigSystem.SpawnRig(entityCommands, character);
            
        // Wide interior shot. DoorApproachSystem parks its pivot on the character.
        var isometricRig = entityCommands
            .Spawn(new CameraRig {
                Pivot = Vector3d.Zero,
                Yaw = Math.PI / 4,
                Pitch = Math.PI / 4,
                Distance = 90.0,
                FieldOfView = 0.4,
                NearPlane = 10.0,
                FarPlane = 500.0
            });

        // Doorway shot: same angles as the wide one, held tight on the character. Every value but the
        // pivot is constant, so blending toward it can only zoom in - and both sides of a portal
        // converge on this exact framing, which is what hides the cut.
        var doorRig = entityCommands
            .Spawn(new CameraRig {
                Pivot = Vector3d.Zero,
                Yaw = 0,
                Pitch = Math.PI / 4,
                Distance = 8.0,
                FieldOfView = 0.4,
                NearPlane = 0.1,
                FarPlane = 500.0
            });

        entityCommands
            .Spawn(new CameraComponent { Priority = 100 })
            .At(Vector3d.Zero)
            .With(new CameraDirector {
                FollowRig = followRig, IsometricRig = isometricRig, DoorRig = doorRig, Target = 0
            });
    }
}