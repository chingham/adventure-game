using System.Numerics;
using AdventureGame.App;
using Quark.Ecs;
using Quark.Graphics;
using Quark.Kit;
using Quark.Kit.Assets;
using Quark.Kit.Components;
using Quark.Kit.Rendering;
using Quark.Kit.Rendering.Features;
using Quark.Kit.Rendering.Particles;
using Quark.Kit.Rendering.PostEffects;
using Quark.Numerics;

namespace AdventureGame.Features.Ambience;

// The feel of a place: how far you see, what it does to colour, and the weather over it. The level
// file authors the values; what they are written to is built here, so the feature stands on its own
// rather than on the file having declared the right effects.
sealed class AmbienceFeature : IGameFeature {
    public void Provide(IServiceRegistry services) {
        services.Add(locator => {
            var rendering = locator.Get<DefaultRenderingModule>();
            var assets = locator.Get<AssetLibrary>();
            var simulation = locator.Get<RigidBodySimulation>();

            return new Ambience(
                AddGrade(rendering),
                AddFog(rendering),
                SpawnRain(locator.Get<World>(), rendering, assets, simulation));
        });
    }

    public void Install(Game game) {
        game.AddSystem<AmbienceSystem>(QuarkPhases.Gameplay, Order.Ambience);
    }

    // What the ambience drives
    //
    // The values here are only the state the game opens on: the level's own aspect takes over on the
    // first frame, and every zone from there.

    static EffectHandle<GradeEffect> AddGrade(DefaultRenderingModule rendering) =>
        rendering.AddPostEffect(new GradeEffect {
            Saturation = 1,
            Warmth = 0
        }, PostEffectSpace.Ldr, fold: true);

    static SurfaceFeatureHandle<DepthFogFeature> AddFog(DefaultRenderingModule rendering) =>
        rendering.MainView.AddSurfaceFeature(new DepthFogFeature {
            Color = Color.FromHex("0E0F15"),
            Density = 0.02f
        });

    // One emitter for the whole level, moved and rated by the system. It is born silent: Rate is what
    // the weather writes, and the level opens on whatever intensity the air around the spawn asks for.
    static Entity SpawnRain(World world, DefaultRenderingModule rendering, AssetLibrary assets, RigidBodySimulation simulation) {
        var texture = assets.LoadTexture(
            "../../../Data/Textures/raindrop.png",
            TextureColorSpace.Srgb,
            new Brightness { Amount = 0.5f });
        
        var material = rendering.CreateMaterial(
            new ParticleMaterial {
                Response = 1,
                Fade = 0,
                Billboard = new BillboardFeature {
                  Align  = BillboardAlign.Velocity,
                  Pivot = new Vector2(0.5f, 1f)
                },
                Atlas = new Vector2(1 + RainDropBehavior.SplashFrames, 1)
            },
            texture);

        return world.Spawn(new ParticleEmitter {
            Prewarm = 2,
            Rate = 1000,
            Emission = EmitShape.Circle(20),
            Spread = 2,
            Lifetime = 2.0f,
            Capacity = 32000,
            Speed = 50,
            Material = material,
            Blend = BlendMode.Alpha,
            StartSize = new Vector2(0.02f, 1),
            Behaviors = [new RainDropBehavior(simulation)]
        }).At(Vector3d.Zero);
    }
}

class RainDropBehavior(RigidBodySimulation simulation) : IParticleBehavior {
    public const int SplashFrames = 3;
    const float SplashSeconds = 0.17f;
    const float SplashSize = 0.125f;
    
    public void Apply(ref Particle particle, in ParticleStep step) {
        // If particle is just born
        if (particle.Age == 0) {
            
            // Raycast scene to find impact point
            var speed = particle.Velocity.Length();
            var direction = particle.Velocity / speed;
            var origin = (Vector3)(step.Anchor + particle.Position);

            if (simulation.Raycast(origin, direction, speed * particle.Life, out var hit)) {
                // Compute Life based on time to impact point
                var impactTime = hit.Distance / speed;
                particle.Life = impactTime + SplashSeconds;
                particle.Custom.W = impactTime;
                return;
            }
        }
        
        // If particle is in splash phase
        if (particle.Custom.W > 0 && particle.Age >= particle.Custom.W) {
            
            // Correct particle position
            if (particle.Velocity.LengthSquared() > 0) {
                var overshootTime = particle.Age - particle.Custom.W;
                var overshootDistance = particle.Velocity * overshootTime;
                particle.Position -= overshootDistance;
            }
            
            // Stop particle and resize it
            particle.Velocity = Vector3.Zero;
            particle.Size = new Vector2(SplashSize, -SplashSize);
            
            // Compute splash frame
            var t = (particle.Age - particle.Custom.W) / SplashSeconds;
            particle.Frame = 1 + (int)Math.Floor(t * SplashFrames);
        }
    }
}
