using AdventureGame.Debug;
using ImGuiNET;
using Quark.Ecs;

namespace AdventureGame.Features.Camera;

// One window for the whole camera: what the director is currently doing, and every value it is doing
// it with. The doorway transition is pure feel, so it is dialled in while walking through the door.
sealed class CameraPanel(CameraTuning tuning) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {
        using var panel = DebugUi.Panel("Camera");
        if (!panel.Open)
            return;

        Director(world);

        if (ImGui.CollapsingHeader("Follow rig"))
            FollowRig();

        if (ImGui.CollapsingHeader("Doorway"))
            Doorway();
    }

    // Readouts

    static void Director(World world) {
        foreach (var row in world.Query<CameraDirector>()) {
            var director = row.Component1;
            ImGui.Text($"Actual target: {director.ActualTarget:F2}");
            ImGui.Text($"Approach: {director.Approach:F2}");
            break;
        }
    }

    // Tuning

    void FollowRig() {
        ImGui.SliderFloat("Min distance", ref tuning.MinDistance, 0.5f, 6);
        ImGui.SliderFloat("Max lead", ref tuning.MaxLead, 0, 5);
        ImGui.SliderFloat("Occlusion decay", ref tuning.OcclusionDecay, 1, 60);
        ImGui.SliderFloat("Deocclusion decay", ref tuning.DeocclusionDecay, 0.1f, 10);
        ImGui.Separator();
        ImGui.SliderFloat("Recentre grace", ref tuning.RecenterGrace, 0, 4);
        ImGui.SliderFloat("Recentre min speed", ref tuning.RecenterMinSpeed, 0, 6);
        ImGui.SliderFloat("Recentre decay", ref tuning.RecenterDecay, 0, 10);
        ImGui.SliderFloat("Recentre deadzone", ref tuning.RecenterDeadzone, 0, 1.5f);
        ImGui.SliderFloat("Recentre ramp", ref tuning.RecenterRamp, 0.01f, 2);
    }

    void Doorway() {
        ImGui.SliderFloat("Approach depth", ref tuning.ApproachDepth, 0, 20);
        ImGui.SliderFloat("Door plateau", ref tuning.DoorPlateau, 0, 20);
        ImGui.SliderFloat("Lateral falloff", ref tuning.LateralFalloff, 0, 20);
        ImGui.SliderFloat("Vertical reach", ref tuning.VerticalReach, 0, 20);
        ImGui.SliderFloat("Focus height", ref tuning.FocusHeight, 0, 20);
        ImGui.Separator();
        ImGui.SliderFloat("Camera pull", ref tuning.CameraPull, 0, 20);
        ImGui.SliderFloat("Basis thaw", ref tuning.BasisThaw, 1, 40);
        ImGui.SliderFloat("Commitment curve", ref tuning.BasisCommitmentCurve, 0.25f, 4);
        ImGui.SliderFloat("Basis min thaw", ref tuning.BasisMinThaw, 0, 10);
    }
}
