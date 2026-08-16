namespace AdventureGame.Features.Character;

// The body itself: what the mesh is built from and what the solver sweeps. Fixed at compile time -
// changing it live would mean respawning the capsule and its collider.
static class CharacterShape {
    public const float CapsuleRadius = 0.4f;
    public const float CapsuleSegmentHeight = 1.0f;
    public const float CapsuleRestHeight = CapsuleRadius + CapsuleSegmentHeight / 2f;

    // Gap kept between the body and everything it touches, so a resting contact never counts as a
    // penetration on the next tick.
    public const float SkinWidth = 0.015f;
}
