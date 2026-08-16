using AdventureGame.Common;
using AdventureGame.Features.Character;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Features.Portals;

// One end of a two-way portal, carrying the frame mapping toward the far end. Crossing transplants
// the character's transform relative to this door onto the far door - offset, heading and velocity
// carry over, so entering off-centre on a diagonal comes out off-centre on the same diagonal.
struct Portal {
    public Vector3d Center;       // this door's reference point, at floor level
    public Vector3d Through;      // legitimate crossing direction; arriving overshoot goes the other way
    public Vector3d ExitCenter;   // far door's reference point
    public double YawDelta;       // turn from this door's frame to the far one's
    public double HalfWidth;      // door opening, for the approach falloff
    public double HalfDepth;      // volume half-thickness, for the staleness check
}

sealed class PortalSystem : ISystem {
    readonly EventReader<TriggerEvent> triggers = new();

    public void Update(World world, EntityCommands commands, float deltaTime) {
        foreach (var evt in triggers.Read(world)) {
            // Stay counts too: someone who stopped inside the volume and moves again still crosses
            if (evt.Kind == TriggerEventKind.Exit)
                continue;
            if (!world.Has<Portal>(evt.Trigger) || !world.Has<CharacterMovement>(evt.Other))
                continue;

            var portal = world.Get<Portal>(evt.Trigger);
            ref var movement = ref world.Get<CharacterMovement>(evt.Other);

            var from = movement.Position;
            var offset = from - portal.Center;

            // The kinematic body trails the transform by a tick or two, so a door just left behind
            // can still report overlaps from where the character used to be. Trust the position, not
            // the event: he is either in this doorway now, or the report is stale.
            if (Math.Abs(Vector3d.Dot(offset, portal.Through)) > portal.HalfDepth + StaleMargin)
                continue;

            // Only a crossing into the door teleports; walking out through the arrival volume is
            // the tail end of a trip, not a new one.
            if (Vector3d.Dot(Utils.FlattenXY(movement.Velocity), portal.Through) <= 0)
                continue;

            var landing = portal.ExitCenter + Utils.TurnZ(offset, portal.YawDelta);
            var yaw = Angle.Wrap(movement.Yaw + portal.YawDelta);

            // Previous* are written too: TeleportedThisTick only lives inside a fixed tick, and the
            // interpolation must not blend across the jump.
            movement.Position = landing;
            movement.PreviousPosition = landing;
            movement.Yaw = yaw;
            movement.VisualYaw = Angle.Wrap(movement.VisualYaw + portal.YawDelta);
            movement.PreviousVisualYaw = movement.VisualYaw;
            movement.Velocity = Utils.TurnZ(movement.Velocity, portal.YawDelta);

            world.Events<CharacterEvents.Teleported>()
                .Write(new CharacterEvents.Teleported(evt.Other, from, landing, portal.YawDelta, evt.Trigger));
        }
    }

    // The capsule reaches into the volume from outside it, so the doorway is a little thicker than
    // the trigger box when judging whether the character is in it.
    const double StaleMargin = 1;
}
