namespace AdventureGame.Systems;

// Whether the player is holding his own reins. A running sequence takes them for the length of a
// gate; the controller and the interaction reader both go quiet meanwhile. Recomputed every frame by
// SequenceSystem from the runners alive, so nothing can leave it stuck on.
sealed class PlayerControl {
    public bool Locked;
}
