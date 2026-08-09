using Quark.Ecs;
using Quark.Kit.Components;
using Quark.Numerics;

namespace AdventureGame.Systems;

enum PlatformMode { Shuttle, Called }
enum PlatformPhase { Idle, Outbound, Dwelling, Inbound }

// Both modes run the same round trip - travel out, pause, travel back, pause at home. They differ only
// in what ends the pause at home: a shuttle leaves on its own, a called platform waits for its signal.
struct MovingPlatform {
    public Vector3d From;   // home
    public Vector3d To;
    public PlatformMode Mode;

    public double Speed;    // cruise speed, m/s
    public double Dwell;    // pause at each stop
    public string? On;      // called: the signal that summons it

    internal double Elapsed;
    internal PlatformPhase Phase;

    public Matrix4x4d PreviousTransform;
    public Matrix4x4d CurrentTransform;
}

// Turns a summon into durable state, in a frame phase.
//
// Signals only travel safely from the fixed phase to a frame phase: within a frame the fixed phase runs
// first, so a frame reader always catches them. The reverse is not true - the fixed phase skips whole
// frames whenever the frame rate outruns the fixed step, and the channel only retains two frames, so an
// event published by a frame system can expire before the next tick ever reads it.
sealed class PlatformCallSystem : ISystem {
    readonly EventReader<Signal> signals = new();

    public void Update(World world, EntityCommands commands, float deltaTime) {
        foreach (var signal in signals.Read(world))
            foreach (var row in world.Query<MovingPlatform>()) {
                ref var platform = ref row.Component1;
                if (platform is { Mode: PlatformMode.Called, Phase: PlatformPhase.Idle } && platform.On == signal.Name) {
                    platform.Phase = PlatformPhase.Outbound;
                    platform.Elapsed = 0;
                }
            }
    }
}

sealed class MovingPlatformSystem : ISystem {
    // Seconds a called platform spends accelerating (and decelerating); cruise fills the middle.
    const double RampTime = 0.6;

    public void Update(World world, EntityCommands commands, float deltaTime) {
        foreach (var row in world.Query<RelativeTransform, MovingPlatform>()) {
            ref var transform = ref row.Component1;
            ref var platform = ref row.Component2;

            if (platform.CurrentTransform == default)
                platform.CurrentTransform = Matrix4x4d.Identity;
            platform.PreviousTransform = platform.CurrentTransform;

            Advance(world, ref transform, ref platform, deltaTime);

            platform.CurrentTransform = transform.LocalTransform.ToMatrix4x4();
        }
    }

    void Advance(World world, ref RelativeTransform transform, ref MovingPlatform platform, float deltaTime) {
        switch (platform.Phase) {
            case PlatformPhase.Idle:
                // A shuttle leaves on its own; a called one waits for PlatformCallSystem to flip it
                if (platform.Mode != PlatformMode.Shuttle)
                    break;
                platform.Elapsed += deltaTime;
                if (platform.Elapsed >= platform.Dwell) {
                    platform.Phase = PlatformPhase.Outbound;
                    platform.Elapsed = 0;
                }
                break;

            case PlatformPhase.Outbound:
                if (Travel(ref transform, ref platform, platform.From, platform.To, deltaTime)) {
                    platform.Phase = PlatformPhase.Dwelling;
                    platform.Elapsed = 0;
                }
                break;

            case PlatformPhase.Dwelling:
                platform.Elapsed += deltaTime;
                if (platform.Elapsed >= platform.Dwell) {
                    platform.Phase = PlatformPhase.Inbound;
                    platform.Elapsed = 0;
                }
                break;

            case PlatformPhase.Inbound:
                if (Travel(ref transform, ref platform, platform.To, platform.From, deltaTime)) {
                    platform.Phase = PlatformPhase.Idle;
                    platform.Elapsed = 0;
                    if (platform.On is { } on)
                        world.Events<Signal>().Write(new Signal(on + ".done"));
                }
                break;
        }
    }

    // Advances along from->to with a trapezoidal speed profile (ease in, cruise, ease out).
    // Returns true when the trip is complete, transform parked exactly on the destination.
    static bool Travel(
        ref RelativeTransform transform, ref MovingPlatform platform,
        Vector3d from, Vector3d to, float deltaTime) {
        platform.Elapsed += deltaTime;

        var length = Vector3d.Distance(from, to);
        if (length <= 0) {
            transform.LocalTransform.Position = to;
            return true;
        }

        var covered = TrapezoidDistance(platform.Elapsed, length, platform.Speed, out var duration);
        transform.LocalTransform.Position = Vector3d.Lerp(from, to, covered / length);
        return platform.Elapsed >= duration;
    }

    // Distance covered after `elapsed` seconds over a trip of `length` at cruise `speed`. Short trips
    // that never reach cruise degrade to a triangular profile.
    static double TrapezoidDistance(double elapsed, double length, double speed, out double duration) {
        var acceleration = speed / RampTime;

        if (length < speed * RampTime) {
            // Triangular: accelerate halfway, decelerate the rest
            var peak = Math.Sqrt(acceleration * length);
            duration = 2 * peak / acceleration;
            var half = duration / 2;
            var s = elapsed <= half
                ? 0.5 * acceleration * elapsed * elapsed
                : length - 0.5 * acceleration * Square(duration - elapsed);
            return Math.Clamp(s, 0, length);
        }

        duration = length / speed + RampTime;
        var distance = elapsed switch {
            _ when elapsed <= RampTime => 0.5 * acceleration * elapsed * elapsed,
            _ when elapsed >= duration - RampTime => length - 0.5 * acceleration * Square(duration - elapsed),
            _ => speed * RampTime / 2 + speed * (elapsed - RampTime)
        };
        return Math.Clamp(distance, 0, length);
    }

    static double Square(double x) => x * x;
}
