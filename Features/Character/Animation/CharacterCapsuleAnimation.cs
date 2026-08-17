using System.Numerics;
using AdventureGame.Common;
using Quark.Ecs;
using Quark.Kit;
using Quark.Kit.Animation.Clips;
using Quark.Kit.Components;
using Quark.Kit.Rendering.Particles;
using Quark.Numerics;

namespace AdventureGame.Features.Character;

struct CharacterCapsuleAnimation {
    public Entity Body;
    
    internal Vector3d lastRootXY;
    internal Vector3d hopFrom;
    internal Vector3d hopTo;
    internal double hopTimer;
    internal double hopLastTimer;
    internal double lean;
}

sealed class CharacterCapsuleAnimationSystem(CharacterTuning tuning) : ISystem {
    readonly EventReader<CharacterEvents.Jumped> jumpedReader = new();
    readonly EventReader<CharacterEvents.Landed> landedReader = new();
    
    public void Update(World world, EntityCommands commands, float deltaTime) {
        foreach (var row in world.Query<RelativeTransform, CharacterAnimParams, CharacterCapsuleAnimation>()) {
            Update(world, row.Entity, row.Component1, row.Component2, ref row.Component3, deltaTime);
        }

        foreach (var e in jumpedReader.Read(world)) {
            var rand = Random.Shared.NextDouble();
            var clip =
                rand < 0.2 ? Jump1() :
                rand < 0.4 ? Jump2() :
                JumpLaunch();
            world.Play(clip, BodyOf(world, e.Entity));
            world.Play(JumpSmoke(), e.Entity);
            world.Play(ImpactSmoke(0.4f), e.Entity);
        }
        foreach (var e in landedReader.Read(world)) {
            world.Play(LandSquash(e.ImpactSpeed), BodyOf(world, e.Entity));
            world.Play(ImpactSmoke(e.ImpactSpeed), e.Entity);
        }
    }

    static Entity BodyOf(World world, Entity entity) {
        ref var capsule = ref world.Get<CharacterCapsuleAnimation>(entity);
        return capsule.Body;
    }

    static Clip JumpLaunch() {
        const float coil = 0.08f;
        const float launch = 0.16f;
        const float settle = 0.22f;
        
        return Clip.Create().Scale(
            Keys.From(Vector3.Zero)
                .To(new Vector3(0.12f, 0.12f, -0.18f), coil, Ease.OutQuad)
                .To(new Vector3(-0.10f, -0.10f, 0.22f), launch, Ease.OutQuad)
                .To(Vector3.Zero, settle, Ease.OutBack),
            TrackSpace.Offset);
    }
    static Clip Jump1() {
        var delay = 0.05f;
        var duration = 0.3f;
        var pivot = new Vector3(0, 0, CharacterShape.CapsuleRestHeight);

        return Clip.Create()
            .Then(delay)
            .Spin(Vector3.UnitX, turns: -1, duration, pivot)
            .Insert(0, JumpLaunch());
    }
    static Clip Jump2() {
        var delay = 0.05f;
        var duration = 0.3f;
        var pivot = new Vector3(0, 0, CharacterShape.CapsuleRestHeight);
        
        return Clip.Create()
            .Then(delay)
            .Spin(Vector3.UnitZ, turns: 1, duration, pivot)
            .Insert(0, JumpLaunch())
            .Insert(delay, Clip.Create().Scale(
                Keys.From(Vector3.Zero)
                    .To(new Vector3(-0.09f, -0.09f, 0.2f), duration * 0.5f, Ease.InOutCubic)
                    .Back(duration, Ease.InOutCubic),
                TrackSpace.Offset));
    }
    Clip LandSquash(double impactSpeed) {
        var strength = (float)(impactSpeed / tuning.TerminalVelocity).Clamp01();
        var scaleV = float.Lerp(1, tuning.LandMaxSquash, strength);
        var scaleH = 1 / MathF.Sqrt(scaleV);
        var duration = float.Lerp(0.1f, 0.2f, strength);
        
        return Clip
            .Create(duration)
            .Scale(
                Keys.From(new Vector3(1), 0)
                    .To(new Vector3(scaleH, scaleH, scaleV), duration / 2)
                    .To(new Vector3(1), duration),
                TrackSpace.Absolute
            );
    }
    static Clip JumpSmoke() {
        return Clip.Create().Burst(7, overrides: new BurstOverrides {
            Emission = EmitShape.Circle(0.1f),
            Spread = 1,
            SpeedScale = 1.2f,
            SizeScale = 0.6f
        });
    }
    Clip ImpactSmoke(double impactSpeed) {
        var strength = (float)(impactSpeed / tuning.TerminalVelocity).Clamp01();
        var count = (int)double.Lerp(5, 20, strength);
        var speed = (float)double.Lerp(0.5, 2, strength);
        var scale = (float)double.Lerp(0.6, 1.1, strength);
        var emitRadius = (float)double.Lerp(0.3, 0.8, strength);
        var emitUpperAngle = (float)double.Lerp(80, 70, strength);
        
        return Clip.Create().Burst(count, overrides: new BurstOverrides {
            Emission = EmitShape.Circle(emitRadius),
            Spread = (emitUpperAngle, 95),
            SpeedScale = speed,
            SizeScale = scale
        });
    }
    Clip WalkSmoke(double speed01) {
        var count = (int)double.Lerp(2, 5, speed01);
        var speed = (float)double.Lerp(0.0, 0.2, speed01);
        
        return Clip.Create().Burst(count, overrides: new BurstOverrides {
            Emission = EmitShape.Circle(0.1f),
            Spread = 20,
            SpeedScale = speed, 
            SizeScale = 0.3f
        });
    }

    void Update(World world, Entity e, in RelativeTransform root, in CharacterAnimParams anim, ref CharacterCapsuleAnimation capsule, float deltaTime) {
        if (deltaTime <= float.Epsilon) return;
        
        ref var body = ref world.Get<RelativeTransform>(capsule.Body);
        
        // Continuous walk, hoping from position to position
        var rootXY = Utils.FlattenXY(root.LocalTransform.Position);
        var lag = (rootXY - capsule.hopTo).Length();
        var moving = anim.State == CharacterMoveState.Grounded && anim.Speed01 > 0.05f;
            
        var velocityXY = (rootXY - capsule.lastRootXY) / deltaTime;
        capsule.lastRootXY = rootXY;

        capsule.hopLastTimer += deltaTime;
        
        var due = lag >= tuning.HopStride || capsule.hopLastTimer >= tuning.HopMaxInterval;
        var settled = capsule.hopTimer >= tuning.HopDuration;
        var settle = lag > tuning.HopSettleLag && !moving;

        if (settled
            && capsule.hopLastTimer >= tuning.HopMinInterval
            && ((moving && due) || settle)) {
            var lead = Vector3d.ClampLength(velocityXY * tuning.HopDuration, tuning.HopStride);
            capsule.hopFrom = capsule.hopTo;
            capsule.hopTo = rootXY + lead;
            capsule.hopTimer = 0;
            settled = false;

            if (anim.State == CharacterMoveState.Grounded) {
                world.Play(WalkSmoke(anim.Speed01), e);
            }
        }

        var visualXY = capsule.hopTo;
        var arc = 0d;
        if (!settled) {
            capsule.hopTimer += deltaTime;
            var span = ((capsule.hopTo - capsule.hopFrom).Length() / tuning.HopStride).Clamp01();
            var t = (capsule.hopTimer / tuning.HopDuration).Clamp01();
            visualXY = Vector3d.Lerp(capsule.hopFrom, capsule.hopTo, Ease.Smooth.Evaluate((float)t));
            arc = Math.Sin(t * Math.PI) * tuning.HopHeight * double.Lerp(0.35, 1, span);
        }

        if (anim.State != CharacterMoveState.Grounded) {
            capsule.hopTo = rootXY;
            capsule.hopTimer = tuning.HopDuration;
            visualXY = rootXY;
            arc = 0;
        }

        var offset = visualXY - rootXY;
        var local = Vector3.Transform((Vector3)offset, Quaternion.Inverse(root.LocalTransform.Rotation));

        //var position = new Vector3d(local.X, local.Y, arc);
        var position = new Vector3d(0, 0, arc);
        
        // Continuous fall stretch
        var stretchV = 1 + (-anim.VerticalSpeed / tuning.TerminalVelocity).Clamp01() * tuning.FallStretch;
        var stretchH = 1 / Math.Sqrt(stretchV);
        
        // Continuous lean in turns
        var turn = Math.Clamp(anim.TurnRate / tuning.TurnSpeed, -1, 1);
        var leanAngle = turn * tuning.LeanMaxAngle * anim.Speed01;
        capsule.lean = Decay.ExpDecay(capsule.lean, leanAngle, tuning.LeanDecay, deltaTime);
        
        var leanRotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, (float)capsule.lean);
        
        // Apply continuous transform
        body.LocalTransform.Position = position;
        body.LocalTransform.Scale = new Vector3d(stretchH, stretchH, stretchV);
        body.LocalTransform.Rotation = leanRotation;
    }
}