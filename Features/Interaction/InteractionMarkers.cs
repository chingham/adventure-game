using Quark.Ecs;
using Quark.Numerics;

namespace AdventureGame.Features.Interaction;

sealed class InteractionMarkers {
    public IList<InteractionMarker> Visible { get; } = [];
}

readonly record struct InteractionMarker(Entity Entity, Vector3d Point, double Presence, bool Selected);