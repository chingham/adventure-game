using AdventureGame.Debug;
using ImGuiNET;
using Quark.Ecs;

namespace AdventureGame.Features.Character;

// What the controller is doing, and every value it is doing it with. The readouts come from the anim
// params rather than the movement: they are the same tick, already reduced to what a human reads.
sealed class CharacterPanel(CharacterTuning tuning) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {
        using var panel = DebugUi.Panel("Character");
        if (!panel.Open)
            return;

        foreach (var row in world.Query<CharacterAnimParams>()) {
            State(row.Component1);
            break;
        }

        if (ImGui.CollapsingHeader("Speed"))
            Speed();

        if (ImGui.CollapsingHeader("Jump"))
            Jump();

        if (ImGui.CollapsingHeader("Ground"))
            Ground();

        if (ImGui.CollapsingHeader("Animation"))
            Animation();
    }

    // Readouts

    static void State(in CharacterAnimParams anim) {
        ImGui.Text($"State: {anim.State}");
        ImGui.Text($"Time in state: {anim.TimeInState:F2} s");
        ImGui.Separator();
        ImGui.Text($"Speed: {anim.Speed01:F2} m/s");
        ImGui.Text($"Vertical speed: {anim.VerticalSpeed:F2} m/s");
        ImGui.Text($"Turn rate: {anim.TurnRate / MathF.PI * 180:F2}°/s");
        ImGui.Text($"Slope angle: {anim.SlopeAngle / MathF.PI * 180:F2}°");
    }

    // Tuning

    void Speed() {
        ImGui.SliderFloat("Max speed", ref tuning.MaxSpeed, 1, 16);
        ImGui.SliderFloat("Time to max speed", ref tuning.TimeToMaxSpeed, 0.01f, 1);
        ImGui.SliderFloat("Time to stop", ref tuning.TimeToStop, 0.01f, 1);
        ImGui.SliderFloat("Air time to max", ref tuning.AirTimeToMax, 0.01f, 2);
        ImGui.Separator();
        ImGui.SliderFloat("Turn speed", ref tuning.TurnSpeed, 1, 40);
        ImGui.SliderFloat("Turn snap below", ref tuning.TurnSnapBelowSpeed, 0, 3);
    }

    void Jump() {
        ImGui.SliderFloat("Height", ref tuning.JumpHeight, 0.2f, 5);
        ImGui.SliderFloat("Time to apex", ref tuning.JumpTimeToApex, 0.05f, 1.2f);
        ImGui.TextDisabled($"rise gravity {tuning.JumpRiseGravity:F1}   launch {tuning.JumpSpeed:F1} m/s");
        ImGui.Separator();
        ImGui.SliderFloat("Fall multiplier", ref tuning.FallMultipler, 1, 4);
        ImGui.SliderFloat("Apex threshold", ref tuning.JumpApexThreshold, 0, 5);
        ImGui.SliderFloat("Apex gravity scale", ref tuning.JumpApexGravityScale, 0, 2);
        ImGui.SliderFloat("Cut gravity scale", ref tuning.JumpCutGravityScale, 1, 5);
        ImGui.SliderFloat("Terminal velocity", ref tuning.TerminalVelocity, 5, 60);
        ImGui.Separator();
        ImGui.SliderFloat("Coyote time", ref tuning.CoyoteTime, 0, 0.4f);
        ImGui.SliderFloat("Jump buffer", ref tuning.JumpBufferTime, 0, 0.4f);
    }

    void Ground() {
        ImGui.SliderAngle("Walkable angle", ref tuning.WalkableAngle, 0, 80);
        ImGui.SliderAngle("Unwalkable angle", ref tuning.UnwalkableAngle, 0, 80);
        ImGui.SliderFloat("Stick to ground", ref tuning.StickToGround, 0, 0.5f);
        ImGui.Separator();
        ImGui.SliderFloat("Step up height", ref tuning.StepUpHeight, 0, 1);
        ImGui.SliderFloat("Step down height", ref tuning.StepDownHeight, 0, 1);
        ImGui.Separator();
        ImGui.SliderFloat("Slide acceleration", ref tuning.SlideAcceleration, 0, 40);
        ImGui.SliderFloat("Slide control", ref tuning.SlideControl, 0, 1);
        ImGui.Separator();
        ImGui.SliderFloat("Max inherited speed", ref tuning.MaxInheritedSpeed, 0, 20);
        ImGui.SliderFloat("Max inherited rise", ref tuning.MaxInheritedRise, 0, 20);
        ImGui.SliderFloat("Carry half-life", ref tuning.CarryHalfLife, 0.05f, 3);
    }

    void Animation() {
        ImGui.SliderFloat("Hop stride", ref tuning.HopStride, 0.1f, 2);
        ImGui.SliderFloat("Hop duration", ref tuning.HopDuration, 0.05f, 0.6f);
        ImGui.SliderFloat("Hop height", ref tuning.HopHeight, 0, 0.6f);
        ImGui.SliderFloat("Hop settle lag", ref tuning.HopSettleLag, 0, 0.3f);
        ImGui.SliderFloat("Hop min interval", ref tuning.HopMinInterval, 0, 0.6f);
        ImGui.SliderFloat("Hop max interval", ref tuning.HopMaxInterval, 0.05f, 1.5f);
        ImGui.Separator();
        ImGui.SliderFloat("Fall stretch", ref tuning.FallStretch, 0, 1);
        ImGui.SliderFloat("Land squash", ref tuning.LandMaxSquash, 0, 1);
        ImGui.SliderFloat("Lean max angle", ref tuning.LeanMaxAngle, 0, 1);
        ImGui.SliderFloat("Lean decay", ref tuning.LeanDecay, 1, 40);
    }
}
