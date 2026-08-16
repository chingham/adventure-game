using AdventureGame.Common;
using Quark.Numerics;

namespace AdventureGame.Features.Character;

// What the controller did, reduced to what an animation needs to know. Written once per fixed step,
// read by everything visual - so nothing on the animation side ever reaches into the solver's state.
struct CharacterAnimParams {
    public double Speed01;
    public double VerticalSpeed;
    public bool Grounded;
    public double TurnRate;
    public double SlopeAngle;
    public CharacterMoveState State;
    public double TimeInState;

    public void ReadFrom(in CharacterMovement movement, CharacterTuning tuning, float deltaTime) {
        // What a platform carries us at is not what we are running at
        var carried = Utils.FlattenXY(movement.carryVelocity).Length();

        Grounded = movement.Grounded;
        Speed01 = Utils.Clamp01((movement.ActualHorizontalSpeed - carried) / tuning.MaxSpeed);
        VerticalSpeed = movement.Grounded ? 0 : movement.Velocity.Z;
        TurnRate = movement.TurnRate;
        SlopeAngle = Math.Acos(Math.Clamp(movement.groundNormalSmoothed.Z, -1, 1));

        // State + time in state
        var state = (movement.Grounded || movement.coyoteTimer > 0) ? CharacterMoveState.Grounded
            : movement.touchedSteep ? CharacterMoveState.Sliding
            : CharacterMoveState.Airborne;

        if (state != State) {
            State = state;
            TimeInState = 0;
        }
        else {
            TimeInState += deltaTime;
        }
    }
}

enum CharacterMoveState {
    Grounded,
    Airborne,
    Sliding,
    Scripted
}
