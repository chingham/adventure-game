namespace AdventureGame.App;

/// <summary>
/// Where each system sits inside its phase. Features register themselves but never pick a number, so
/// this file stays the one place the frame can be read in order.
/// </summary>
static class Order {
    // Input: the flow decides what is live before anything reads it
    public const int Flow = 0;
    public const int CursorToggle = 10;
    public const int LevelReload = 20;

    // Simulation (fixed step): what carries the character moves first, the character last
    public const int MovingPlatform = 5;
    public const int InputBasis = 10;
    public const int CharacterIntent = 110;
    public const int CharacterMovement = 120;

    // Gameplay: sense the world, run the scripts, then place the camera on the result
    public const int Interaction = -20;
    public const int Trigger = -19;
    public const int Sequence = -18;
    public const int Probe = -17;
    public const int PlatformCall = -10;
    public const int Portal = -10;
    public const int Space = -7;
    public const int DoorApproach = -6;
    public const int FollowRig = -5;
    public const int CameraDirector = -4;

    // Late update: the visual pass, then the panels drawn on top of it
    public const int CharacterInterpolation = -10;
    public const int CharacterAnimation = -8;
    public const int Showroom = 5;
    public const int Panel = 0;

    // Render submit
    public const int DebugVolumes = 10;
}
