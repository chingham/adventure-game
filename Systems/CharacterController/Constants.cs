namespace AdventureGame.Systems.CharacterController;

public class Constants {
    // Character
    
    public const float CapsuleRadius = 0.4f;
    public const float CapsuleSegmentHeight = 1.0f;
    public const float CapsuleRestHeight = CapsuleRadius + CapsuleSegmentHeight / 2f;
    public const float SkinWidth = 0.015f;

    public const float WalkableAngle = 44.0f * MathF.PI / 180;
    public const float UnwalkableAngle = 49.0f * MathF.PI / 180;
    public static readonly float WalkableCos = MathF.Cos(WalkableAngle);
    public static readonly float UnwalkableCos = MathF.Cos(UnwalkableAngle);

    public const float SteepMinZ = 0.1f;

    public const float JumpHeight = 1.65f;
    public const float JumpTimeToApex = 0.32f;
    public const float JumpRiseGravity = 2 * JumpHeight / (JumpTimeToApex * JumpTimeToApex);
    public const float JumpSpeed = 2 * JumpHeight / JumpTimeToApex;
    public const float FallMultipler = 1.6f;
    public const float JumpApexThreshold = 1.5f;
    public const float JumpApexGravityScale = 0.55f;
    public const float TerminalVelocity = 28f;
    public const float StickToGround = 0.05f;
    
    public const float MaxSpeed = 6.0f;
    public const float TimeToMaxSpeed = 0.15f;
    public const float TimeToStop = 0.08f;
    public const float AirTimeToMax = 0.35f;

    public const float CoyoteTime = 0.11f;
    public const float JumpBufferTime = 0.18f;
    public const float JumpCutGravityScale = 2.2f;

    public const float TurnSpeed = 14f;
    public const float TurnSnapBelowSpeed = 0.6f;

    public const float SlideAcceleration = 16f;
    public const float SlideControl = 0.25f;

    public const float StepUpHeight = 0.30f;
    public const float StepDownHeight = 0.35f;
    public const float StepLedgeMargin = 0.05f;
    public const float StepLedgeMinRise = 0.05f;
    public const int StepUpGraceTicks = 3;

    public const float NudgeDistance = 0.14f;

    public const float MaxInheritedSpeed = 6;
    public const float MaxInheritedRise = 5;
    public const int CrushStuckTicks = 10;
    public const float CarryHalfLife = 0.6f;
    public const float CrushTolerance = 0.05f;
    public const float MaxDepenetrationPerTick = 0.5f;
    
    // Camera

    public const float CameraMinDistance = 1.3f;
    public const float CameraMaxLead = 1.2f;
    public const float CameraRecenterGrace = 1.0f;
    public const float CameraRecenterMinSpeed = 0.8f;
    public const float CameraRecenterDecay = 1.8f;
    public const float CameraRecenterDeadzone = 0.22f;
    public const float CameraRecenterRamp = 0.3f;
    public const float CameraOcclusionDecay = 30f;
    public const float CameraDeocclusionDecay = 1.4f;
    
    // Animation
    
    public const float StrideLength = 1.2f;
    public const float BobHeight = 0.3f;
    public const float HopStride = 0.5f;
    public const float HopSettleLag = 0.03f;
    public const float HopDuration = 0.15f;
    public const float HopHeight = 0.12f;
    public const float HopMinInterval = 0.12f;
    public const float HopMaxInterval = 0.35f;
    public const float FallStretch = 0.15f;
    public const float LeanMaxAngle = 0.15f;
    public const float LeanDecay = 16f;
    public const float LandMaxSquash = 0.3f;
}