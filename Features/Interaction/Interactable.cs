using AdventureGame.App;
using AdventureGame.Common;
using AdventureGame.Features.Character;
using AdventureGame.Features.Progression;
using AdventureGame.Features.Sequences;
using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Features.Interaction;

// Something the character can act on: a point offset from its object, with a reach. No volume, so it
// rides any entity, still or moving, and costs one modifier to author. Using it emits `Emit` on the
// signal bus; what answers is the bus's business, exactly as for a button.
struct Interactable() {
    public string Prompt = "";
    public string Emit = "";
    public Vector3d Offset;       // where the hand lands, from the entity's origin, turning with it
    public double Reach;
    public bool Once;             // retires for good once used, remembered outside the world
    public bool Enabled = true;   // orthogonal pause: a sequence can silence it and hand it back
}

// Rides whoever can use things, and holds what he has in reach right now.
struct Interactor() {
    public Entity Candidate;

    public double ReachCone = 120;
    public double VerticalReach = 2;
    public FloatRange MarkerRange = new(6, 8);
}

sealed class InteractionSystem(IInput input, Flags flags, InteractionMarkers markers) : ISystem {
    public static string UsedFlag(string emit) => emit + ".used";

    public void Update(World world, EntityCommands commands, float deltaTime) {
        var use = input.Consume(Controls.Interact);
        
        markers.Visible.Clear();

        foreach (var row in world.Query<CharacterMovement, Interactor>()) {
            ref var movement = ref row.Component1;
            ref var interactor = ref row.Component2;
            
            // Find new candidate
            var previousCandidate = interactor.Candidate;
            var newCandidate = Select(world, movement, interactor, markers);
            
            // Assign new candidate, changing render layers
            if (previousCandidate != newCandidate) {
                interactor.Candidate = newCandidate;
                SetHighlighted(world, markers, previousCandidate, highlighted: false);
                SetHighlighted(world, markers, newCandidate, highlighted: true);
            }
            
            // Update marker selected state
            for (var i = 0; i < markers.Visible.Count; i++) {
                var marker = markers.Visible[i];
                if (marker.Entity != interactor.Candidate) continue;
            
                // Update marker selection
                markers.Visible[i] = marker with { Selected = true };
                break;
            }

            // Use item
            if (use && !interactor.Candidate.IsNull) {
                // Get interactable
                var interactable = world.Get<Interactable>(interactor.Candidate);
                
                // Set flag if one-shot
                if (interactable.Once)
                    flags.Set(UsedFlag(interactable.Emit));

                // Emit signal
                world.Events<Signal>().Write(new Signal(interactable.Emit, interactor.Candidate));
            }
        }
    }

    Entity Select(World world, in CharacterMovement movement, in Interactor interactor, InteractionMarkers markers) {
        // Store best candidate
        var facing = new Vector3d(Math.Sin(movement.Yaw), Math.Cos(movement.Yaw), 0);
        var best = Entity.Null;
        var bestScore = double.MaxValue;

        // For each interactable
        foreach (var row in world.Query<Interactable, RelativeTransform>()) {
            
            // Filter if disabled or already used
            var interactable = row.Component1;
            if (!interactable.Enabled || interactable.Once && flags.Has(UsedFlag(interactable.Emit)))
                continue;

            // Compute world point
            var transform = row.Component2.LocalTransform;
            var point = transform.Position + Vector3d.Transform(interactable.Offset, transform.Rotation);
            var toPoint = point - movement.Position;

            // Compute distance
            var flat = Utils.FlattenXY(toPoint);
            var distance = interactor.MarkerRange.Max * 2;
            if (Math.Abs(toPoint.Z) <= interactor.VerticalReach) {
                distance = (float)flat.Length();
            }
            
            // Add marker
            var presence = 1 - interactor.MarkerRange.Progress(distance).Clamp01();
            var marker = new InteractionMarker(row.Entity, point, presence, false);
            markers.Visible.Add(marker);
            
            // Filter if out of reach
            if (distance > interactable.Reach)
                continue;

            // Standing right on it has no direction to judge, so the cone only applies at a distance
            if (distance > Utils.Epsilon && Vector3d.Dot(flat / distance, facing) < Math.Cos(interactor.ReachCone.ToRadians()))
                continue;

            // Replace best based on score (distance)
            var score = distance / interactable.Reach;
            if (score >= bestScore)
                continue;
            
            bestScore = score;
            best = row.Entity;
        }

        return best;
    }

    static void SetHighlighted(World world, InteractionMarkers markers, Entity entity, bool highlighted) {
        if (entity.IsNull) return;
        
        // Find mesh
        if (world.Has<RenderMesh>(entity)) {
            ref var mesh = ref world.Get<RenderMesh>(entity);

            // Get mask without highlight layer, and add it back if highlighted
            var mask = mesh.Layers.Without(Layers.Render.Highlight);
            if (highlighted)
                mask |= Layers.Render.Highlight;

            // Update layer mask
            mesh.Layers = mask;
        }
    }
}
