using ImGuiNET;
using Quark.Ecs;

namespace AdventureGame.Features.Camera;

// The doorway transition is pure feel: how hard the door pulls the camera round, and how hard the
// input basis resists that pull. The two fight each other by design - the camera wants to face the
// door, the basis wants the stick to keep meaning what it meant - so they are tuned together, live.
sealed class DoorTuning {
    // How fast a door swings the camera onto its axis, at full approach
    public float CameraPull = 8;

    // How fast a held basis rejoins the camera once past the doorway
    public float BasisThaw = 35;

    // Shapes how a partial push resists: 1 is linear, higher keeps the hold firm until near neutral,
    // below 1 loosens it early.
    public float BasisCommitmentCurve = 1;

    // Never hold the basis completely: a trickle of convergence keeps what the player sees and what
    // the stick does from drifting too far apart on a long run at the door.
    public float BasisMinThaw = 2.5f;
}

sealed class DoorTuningPanel(DoorTuning tuning) : ISystem {
    public void Update(World world, EntityCommands commands, float deltaTime) {
        if (!ImGui.Begin("Door Transition")) {
            ImGui.End();
            return;
        }

        ImGui.SliderFloat("Camera pull", ref tuning.CameraPull, 0, 20);
        ImGui.Separator();
        ImGui.SliderFloat("Basis thaw", ref tuning.BasisThaw, 1, 40);
        ImGui.SliderFloat("Commitment curve", ref tuning.BasisCommitmentCurve, 0.25f, 4);
        ImGui.SliderFloat("Basis min thaw", ref tuning.BasisMinThaw, 0, 10);

        ImGui.End();
    }
}
