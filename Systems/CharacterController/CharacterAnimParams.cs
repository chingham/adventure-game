namespace AdventureGame.Systems.CharacterController;

struct CharacterAnimParams {
    public double Speed01;
    public double VerticalSpeed;
    public bool Grounded;
    public double TurnRate;
    public double SlopeAngle;
    public CharacterMoveState State;
    public double TimeInState;
}

enum CharacterMoveState {
    Grounded,
    Airborne,
    Sliding,
    Scripted
}