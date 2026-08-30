using System.Numerics;
using AdventureGame.App;
using Quark.Ecs;
using Quark.Graphics;
using Quark.Kit;
using Quark.Kit.Assets;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Features;
using Quark.Kit.Rendering.Particles;
using Quark.Kit.Rendering.PostEffects;
using Quark.Numerics;

namespace AdventureGame.Features.Camera;

// The cameras that watch the world: the follow rig, the doorway pull, and the director blending
// between them.
sealed class CameraFeature : IGameFeature {
    public void Provide(IServiceRegistry services) {
        services.Add<CameraTuning>();
        
        // Ambience service
        services.Add(locator => {
            var rendering = locator.Get<DefaultRenderingModule>();
            var view = rendering.MainView;
            var world = locator.Get<World>();
            var assets = locator.Get<AssetLibrary>();
            
            // Grade
            var grade = rendering.AddPostEffect(new GradeEffect {
                Saturation = 1,
                Warmth = 0
            }, PostEffectSpace.Ldr, fold: true);
            
            // Fog as a surface feature
            var fog = view.AddSurfaceFeature(new DepthFogFeature {
                Color = Color.FromHex("0E0F15"), 
                Density = 0.02f
            });
            
            // Rain as a particle emitter
            var rainTexture = assets.LoadTexture("../../../Data/Textures/raindrop.png");
            var rainMaterial = rendering.CreateMaterial(
                new ParticleMaterial {
                    Response = 1, 
                    Fade = 0.8f,
                    Billboard = BillboardAlign.Velocity
                },
                rainTexture);
            
            var rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 5f.ToRadians())
                * Quaternion.CreateFromAxisAngle(Vector3.UnitX, 180f.ToRadians());
            
            var entity = world.Spawn(new ParticleEmitter {
                Prewarm = 10,
                Rate = 1000,
                Emission = EmitShape.Circle(20),
                Spread = 2,
                Lifetime = 2.0f,
                Capacity = 32000,
                Speed = 50,
                Material = rainMaterial,
                Blend = BlendMode.Alpha,
                StartSize = new Vector2(0.02f, 1)
            })
            .At(new Vector3d(-13, -12, 30), rotation);
            
            // Create service
            return new Ambience(grade, fog, entity);
        });
    }

    public void Install(Game game) {
        
        // Zones first: what follows frames and fogs whatever they resolved to
        game.AddSystem<CameraZoneSystem>(QuarkPhases.Gameplay, Order.Zones);
        game.AddSystem<FogZoneSystem>(QuarkPhases.Gameplay, Order.Zones);
        game.AddSystem<GradeZoneSystem>(QuarkPhases.Gameplay, Order.Zones);
        game.AddSystem<DoorApproachSystem>(QuarkPhases.Gameplay, Order.DoorApproach);
        game.AddSystem<FollowRigSystem>(QuarkPhases.Gameplay, Order.FollowRig);
        game.AddSystem<CameraDirectorSystem>(QuarkPhases.Gameplay, Order.CameraDirector);
    }
}
