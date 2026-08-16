using Quark.Kit.Components;

namespace AdventureGame.App;

static class Layers {
    public static readonly PhysicsLayer Environment = new(2);
    public static readonly PhysicsLayer Character = new(4);

    // Trigger volumes: overlap events only, and the controller sweeps ignore them (they test Environment).
    public static readonly PhysicsLayer Trigger = new(8);
}