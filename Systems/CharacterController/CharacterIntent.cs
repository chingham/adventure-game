using System.Numerics;
using Quark.Ecs;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Systems.CharacterController;

/*
 * Character intent
 * Updates character intent from input, for future use by character controller
 */

struct CharacterIntent() {
    public Vector3d Direction;
    public double Speed;
    public bool JumpPressed;
    public bool JumpHeld;
}

sealed class CharacterIntentSystem(IInput input) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {

        // Get input; Consume takes the press edge exactly once, whatever the fixed step does. A
        // sequence holding the reins drains the press all the same, so it cannot fire on release.
        var jumpPressed = input.Consume(Controls.Jump);
        var jumpHeld = input.Held(Controls.Jump);
        var move = input.Axis(Controls.Move);

        // Apply intent to entities
        foreach (var row in world.Query<InputBasis, CharacterIntent>()) {
            ref var basis = ref row.Component1;
            ref var intent = ref row.Component2;

            var sin = Math.Sin(basis.Yaw);
            var cos = Math.Cos(basis.Yaw);
            var forward = new Vector3d(sin, cos, 0);
            var right = new Vector3d(cos, -sin, 0);

            var dir = move.X * right + move.Y * forward;
            var len = dir.Length();
            if (len > Utils.Epsilon) dir /= len;
            
            intent.Direction = dir;
            intent.Speed = Math.Min(len, 1) * Constants.MaxSpeed;
            intent.JumpPressed = jumpPressed;
            intent.JumpHeld = jumpHeld;
        }
    }
}