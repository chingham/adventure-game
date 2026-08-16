using AdventureGame.App;
using AdventureGame.Common;
using AdventureGame.Features.Camera;
using Quark.Ecs;
using Quark.Platform.Input;

namespace AdventureGame.Features.Character;

/*
 * Input basis
 * Updates the camera yaw for future camera-related gameplay inputs
 */

struct InputBasis {
    public double Yaw;

    // Set while the basis is still catching up to the camera after a doorway held it back
    internal bool Frozen;
}

sealed class InputBasisSystem(IInput input, DoorTuning tuning) : ISystem {
    readonly EventReader<CharacterEvents.Teleported> teleports = new();

    public void Update(World world, EntityCommands commands, float deltaTime) {
        // The world turned under the character: his held direction has to turn with it
        var worldTurn = 0d;
        foreach (var evt in teleports.Read(world))
            worldTurn += evt.YawDelta;

        // Find camera orientation, and whether a door is currently driving it
        var yaw = 0d;
        var steered = false;
        foreach (var row in world.Query<CameraDirector>()) {
            yaw = row.Component1.Yaw;
            steered = row.Component1.Approach > 0;
            break;
        }

        // How much direction the player is actually asking for: it measures his commitment to the
        // line he is on, and so how much a turning camera would betray him by re-aiming it.
        var commitment = Utils.Clamp01(input.Axis(Controls.Move).Length());

        foreach (var row in world.Query<InputBasis>()) {
            ref var basis = ref row.Component1;
            basis.Yaw = Utils.WrapAngle(basis.Yaw + worldTurn);

            // Asking for nothing: take the live basis right away. It costs nothing now and it is
            // what makes the next push read as "up is up", wherever the camera has gone.
            if (commitment <= 0) {
                basis.Yaw = yaw;
                basis.Frozen = false;
                continue;
            }

            // Player-driven camera turns always reproject - that is what makes the camera feel
            // alive. Only a doorway steering it holds the basis back.
            if (!steered && !basis.Frozen) {
                basis.Yaw = yaw;
                continue;
            }

            // Under a doorway the hold is as firm as the push: a committed run keeps its line
            // through the whole turn, a hesitant one is not left facing the wrong way. Once past
            // the doorway the basis rejoins the camera whatever the push.
            basis.Frozen = true;
            var resistance = Math.Pow(commitment, tuning.BasisCommitmentCurve);
            var rate = steered
                ? Math.Max(tuning.BasisMinThaw, tuning.BasisThaw * (1 - resistance))
                : tuning.BasisThaw;
            var delta = Utils.WrapAngle(yaw - basis.Yaw);
            basis.Yaw = Utils.WrapAngle(basis.Yaw + delta * (1 - Utils.Exp2(-deltaTime * rate)));

            if (!steered && Math.Abs(delta) < Utils.Epsilon)
                basis.Frozen = false;
        }
    }
}
