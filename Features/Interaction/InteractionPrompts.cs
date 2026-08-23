using System.Numerics;
using AdventureGame.App;
using Quark.Ecs;
using Quark.Graphics;
using Quark.Kit.Rendering;
using Quark.Kit.Ui;
using Quark.Numerics;
using Quark.Platform.Input;
using InputGlyphs = AdventureGame.Input.InputGlyphs;

namespace AdventureGame.Features.Interaction;

sealed class InteractionPrompts(
    InteractionMarkers markers,
    InputGlyphs glyphs,
    IRenderer renderer,
    IInput input) : IUiRecipe {
    
    public void Compose(UiComposer ui) {

        var camera = renderer.Camera;
        var hasInteractionImage = glyphs.TryGetImage(Controls.Interact, out var interactionImage);
        var held = input.Held(Controls.Interact);
        
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
                Marker(ui,
                    image: hasInteractionImage ? interactionImage : null,
                    invert: held || marker.Active < 1,
                    position: screen, 
                    presence: (float)marker.Presence, 
                    selected: state.Value,
                    farewell: (float)marker.Active);
            }
        }
    }

    void Marker(UiComposer ui, UiImage? image, bool invert, Vector2 position, float presence, float selected, float farewell) {
        const float presenceSize = 18f;
        const float promptSize = 29f;
        const float promptKeyBorderRadius = 6f;
        const float glyphMargin = -2f;
        const float farewellHold = 0.5f;

        var scale = farewell > farewellHold ? 1 : farewell / farewellHold;
        var targetSize = float.Lerp(presenceSize * presence.Clamp01(), promptSize, selected) * scale;
        var radius = input.Scheme == InputScheme.KeyboardMouse && selected > 0.5f ? promptKeyBorderRadius : targetSize / 2;
        var rect = new Rect(
            position.X - targetSize / 2,
            position.Y - targetSize / 2,
            position.X + targetSize / 2, 
            position.Y + targetSize / 2);

        using (ui.Node().At(rect).Enter()) {
            var fillColor = (invert ? Color.White : Color.Black).WithAlpha(selected);
            var imageColor = invert ? Color.Black : Color.White;
            //Console.WriteLine($"Invert: {invert}, Selected: {selected}, HasImage: {image != null}, ImageColor: {imageColor}, FillColor: {fillColor}");
            
            ui.DrawRect(radius)
                .Stroke(Color.White.WithAlpha(1 - selected), 1)
                .Color(fillColor)
                .OuterShadow(0x00000044, 8, 0, new Vector2(0, 1));

            if (image != null) {
                var imageNode = ui
                    .Node(Size.Pixels(rect.Width - glyphMargin * 2), Size.Pixels(rect.Height  - glyphMargin * 2))
                    .Margin(glyphMargin)
                    .Opacity(selected);
                using (imageNode.Enter()) {
                    ui.DrawImage(image.Value).Color(imageColor);
                }
            }
        }

        //ui.DrawRect(rect).Stroke(0xFF0000FF, 1).Color(0x00000000);
    }

    // Marker spring states
    readonly Spring spring = Spring.FromDuration(0.17f, 0.55f);

    readonly Dictionary<Entity, SpringState> markerStates = [];

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