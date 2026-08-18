using System.Numerics;
using AdventureGame.App;
using Quark.Ecs;
using Quark.Graphics;
using Quark.Kit.Rendering;
using Quark.Kit.Ui;
using Quark.Numerics;
using Quark.Platform.Input;

namespace AdventureGame.Features.Interaction;

sealed class InteractionPrompts(InteractionMarkers markers, IRenderer renderer, IInput input) : IUiRecipe {

    const float PresenceCircleSize = 12f;
    const float PromptCircleSize = 16f;
    
    public void Compose(UiComposer ui) {

        var camera = renderer.Camera;
        var label = input.Prompt(Controls.Interact).Label;
        
        // Draw the circle exactly at the projected position
        using (ui.Portal()) {
            // For each interactable with a marker
            foreach (var marker in markers.Visible) {
                
                // Skip if not present
                if (marker.Presence <= 0.01) continue;
                
                // Compute screen position
                var point = marker.Point;
                if (!CameraProjection.Project(camera, point, ui.Surface.X, ui.Surface.Y, out var screen)) continue;
                
                // Get selection state
                var state = GetMarkerState(marker.Entity);
                spring.Update(ref state.Value, ref state.Velocity, marker.Selected ? 1 : 0, ui.DeltaTime);
                SetMarkerState(marker.Entity, state);
                
                // Draw marker
                MarkerCircle(ui, screen, (float)marker.Presence, state.Value);
            }
        }
    }

    static void MarkerCircle(UiComposer ui, Vector2 position, float presence, float selected) {
        var targetRadius = float.Lerp(PresenceCircleSize, PromptCircleSize, selected);
        var radius = targetRadius / 2 * presence.Clamp01();
        var rect = new Rect(position.X - radius, position.Y - radius, position.X + radius, position.Y + radius);
        
        var fillColor = Color.Lerp(Color.FromHex("#FFFFFF00"), Color.FromHex("#FFFFFFFF"), selected);
        ui.DrawRect(rect, radius)
            .Stroke(0xFFFFFFFF, 1)
            .Color(fillColor)
            .OuterShadow(0x00000044, 8, 0, new Vector2(0, 1));
    }

    // Marker spring states
    readonly Spring spring = Spring.FromDuration(0.2f, 0.7f);
    
    readonly Dictionary<Entity, SpringState> markerStates = new();

    struct SpringState(float value, float velocity) {
        public float Value = value;
        public float Velocity = velocity;
    }
    
    SpringState GetMarkerState(Entity entity) {
        if (markerStates.TryGetValue(entity, out var state)) return state;
        state = new SpringState(0, 0);
        markerStates[entity] = state;
        return state;
    }
    void SetMarkerState(Entity entity, SpringState state) {
        markerStates[entity] = state;
    }
}