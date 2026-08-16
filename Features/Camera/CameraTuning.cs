namespace AdventureGame.Features.Camera;

/// <summary>
/// Everything about how the camera feels, in one place and live: what the follow rig does when the
/// player stops steering it, and how a doorway takes the framing over. Read every frame by the
/// systems, written by the panel while the game runs.
/// </summary>
sealed class CameraTuning {
    // Follow rig
    public float MinDistance = 1.3f;
    public float MaxLead = 1.2f;
    public float OcclusionDecay = 30f;
    public float DeocclusionDecay = 1.4f;

    // Lazy recentre: how long the camera waits after the player lets go, and how hard it drifts back
    public float RecenterGrace = 1.0f;
    public float RecenterMinSpeed = 0.8f;
    public float RecenterDecay = 1.8f;
    public float RecenterDeadzone = 0.22f;
    public float RecenterRamp = 0.3f;

    // Doorway reach: metres before the door where the transition starts, and how far past the opening
    // it fades out sideways. The plateau is fully engaged: the trigger always fires inside it, so the
    // cut only ever happens under a fully committed doorway shot - that is what hides the base swap.
    public float ApproachDepth = 8f;
    public float DoorPlateau = 0.5f;
    public float LateralFalloff = 4.0f;
    public float VerticalReach = 3f;
    public float FocusHeight = 1.3f;

    // Doorway pull. The camera wants to face the door, the input basis wants the stick to keep meaning
    // what it meant: the two fight each other by design, so they are tuned together.
    public float CameraPull = 8;

    // How fast a held basis rejoins the camera once past the doorway
    public float BasisThaw = 35;

    // Shapes how a partial push resists: 1 is linear, higher keeps the hold firm until near neutral,
    // below 1 loosens it early.
    public float BasisCommitmentCurve = 1;

    // Never hold the basis completely: a trickle of convergence keeps what the player sees and what
    // the stick does from drifting too far apart on a long run at the door.
    public float BasisMinThaw = 2.5f;
}
