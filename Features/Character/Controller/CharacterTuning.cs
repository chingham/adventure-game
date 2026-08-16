namespace AdventureGame.Features.Character;

/// <summary>
/// How the character feels, live. Everything here is scrubbed while playing rather than recompiled,
/// which is the only way a jump arc or a hop rhythm ever gets dialled in. What the values are derived
/// from is authored, not the derivation: an apex time moves its own gravity.
/// </summary>
sealed class CharacterTuning {
    // Ground
    public float WalkableAngle = 44.0f * MathF.PI / 180;
    public float UnwalkableAngle = 49.0f * MathF.PI / 180;

    public float WalkableCos => MathF.Cos(WalkableAngle);
    public float UnwalkableCos => MathF.Cos(UnwalkableAngle);

    // Kept pressed against the ground over a crest, so a slope does not launch the body
    public float StickToGround = 0.05f;

    public float StepUpHeight = 0.30f;
    public float StepDownHeight = 0.35f;

    // Speed
    public float MaxSpeed = 6.0f;
    public float TimeToMaxSpeed = 0.15f;
    public float TimeToStop = 0.08f;
    public float AirTimeToMax = 0.35f;

    // Jump. Height and time to apex are what a designer thinks in; the gravity and the launch speed
    // follow from them.
    public float JumpHeight = 1.65f;
    public float JumpTimeToApex = 0.32f;

    public float JumpRiseGravity => 2 * JumpHeight / (JumpTimeToApex * JumpTimeToApex);
    public float JumpSpeed => 2 * JumpHeight / JumpTimeToApex;

    public float FallMultipler = 1.6f;
    public float JumpApexThreshold = 1.5f;
    public float JumpApexGravityScale = 0.55f;
    public float TerminalVelocity = 28f;

    // Forgiveness: a jump pressed just late, and one pressed just early, both still land
    public float CoyoteTime = 0.11f;
    public float JumpBufferTime = 0.18f;
    public float JumpCutGravityScale = 2.2f;

    // Turning
    public float TurnSpeed = 14f;
    public float TurnSnapBelowSpeed = 0.6f;

    // Slopes too steep to stand on
    public float SlideAcceleration = 16f;
    public float SlideControl = 0.25f;

    // What a moving platform hands over when the player leaves it
    public float MaxInheritedSpeed = 6;
    public float MaxInheritedRise = 5;
    public float CarryHalfLife = 0.6f;

    // Animation
    public float StrideLength = 1.2f;
    public float BobHeight = 0.3f;
    public float HopStride = 0.5f;
    public float HopSettleLag = 0.03f;
    public float HopDuration = 0.15f;
    public float HopHeight = 0.12f;
    public float HopMinInterval = 0.12f;
    public float HopMaxInterval = 0.35f;
    public float FallStretch = 0.15f;
    public float LeanMaxAngle = 0.15f;
    public float LeanDecay = 16f;
    public float LandMaxSquash = 0.3f;
}
