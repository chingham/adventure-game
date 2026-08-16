using System.Numerics;
using AdventureGame.Common;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Features.Character;

/*
 * Character interpolation
 * Moves the character to an interpolated location between fixed steps.
 */

sealed class CharacterInterpolationSystem(SystemPhase simulationPhase) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {
        var alpha = (float)world.PhaseAlpha(simulationPhase);
        
        // Update characters
        foreach (var row in world.Query<RelativeTransform, CharacterMovement>()) {
            ref var transform = ref row.Component1;
            ref var movement = ref row.Component2;
            
            UpdateCharacter(ref transform, ref movement, alpha);
        }
    }

    static void UpdateCharacter(
        ref RelativeTransform transform, 
        ref CharacterMovement movement,
        float alpha) {
        
        // Interpolate based on phase alpha
        var position = Vector3d.Lerp(movement.PreviousPosition, movement.Position, alpha);
        var yaw = Angle.Lerp(movement.PreviousVisualYaw, movement.VisualYaw, alpha);
        
        var facing = Quaternion.CreateFromAxisAngle(Vector3.UnitZ, -(float)yaw);
        transform.LocalTransform = new Transform(position, facing);
    }
}