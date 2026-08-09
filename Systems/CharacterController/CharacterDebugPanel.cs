using ImGuiNET;
using Quark.Ecs;

namespace AdventureGame.Systems.CharacterController;

public class CharacterDebugPanel : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {
        foreach (var row in world.Query<CharacterIntent, CharacterMovement, CharacterAnimParams>()) {
            var intent = row.Component1;
            var movement = row.Component2;
            var anim = row.Component3;
            
            Draw(movement, intent, anim);
        }
    
    }
    
    void Draw(in CharacterMovement movement, in CharacterIntent intent, in CharacterAnimParams anim) {
        if (!ImGui.Begin("Character Controller")) {
            ImGui.End();
            return;
        }

        ImGui.Text($"State: {anim.State}");
        ImGui.Text($"Time in state: {anim.TimeInState:F2} s");
        ImGui.Separator();
        ImGui.Text($"Speed: {anim.Speed01:F2} m/s");
        ImGui.Text($"Vertical speed: {anim.VerticalSpeed:F2} m/s");
        ImGui.Text($"Turn rate: {anim.TurnRate / MathF.PI * 180:F2}°/s");
        ImGui.Separator();
        ImGui.Text($"Slope angle: {anim.SlopeAngle / MathF.PI * 180:F2}°");
        

        /*ImGui.Text(movement.Grounded ? "Grounded" : "Airborne");
        ImGui.Text($"Ground normal Z: {movement.GroundNormal.Z:F3}");
        ImGui.Text($"Actual horizontal speed: {movement.ActualHorizontalSpeed:F2}");
        ImGui.Separator();
        ImGui.Text($"Z: {movement.Position.Z:F3}");
        ImGui.Text($"Velocity Z: {movement.Velocity.Z:F2}");
        ImGui.Separator();
        ImGui.Text($"Teleported: {movement.TeleportedThisTick}");
        ImGui.Text($"Stuck Ticks: {movement.stuckTicks}");
        ImGui.Separator();
        ImGui.Text($"Slide iterations: {movement.slideIterations}");
        ImGui.Text($"Exhausted iterations: {movement.exhaustedPasses}");
        ImGui.Separator();
        ImGui.Text($"Coyote timer: {movement.coyoteTimer:F2}");
        ImGui.Text($"Jump buffer timer: {movement.bufferTimer:F2}");
        ImGui.Separator();
        ImGui.Text($"Steep normal: {movement.steepNormal}");
        ImGui.Text($"Touched steep: {movement.touchedSteep}");
        ImGui.Separator();
        ImGui.Text($"Step ups: {movement.stepUpsThisTick}   grace: {movement.stepUpGrace}");*/

        ImGui.End();
    }
}