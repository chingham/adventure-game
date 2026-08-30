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

namespace AdventureGame.Features.Ambience;

// The feel of a place: how far you see, what it does to colour, and the weather over it. The level
// file authors the values; what they are written to is built here, so the feature stands on its own
// rather than on the file having declared the right effects.
sealed class AmbienceFeature : IGameFeature {
    public void Provide(IServiceRegistry services) {
        services.Add(locator => {
            var rendering = locator.Get<DefaultRenderingModule>();

            return new Ambience(
                AddGrade(rendering),
                AddFog(rendering),
                SpawnRain(locator.Get<World>(), rendering, locator.Get<AssetLibrary>()));
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
    static Entity SpawnRain(World world, DefaultRenderingModule rendering, AssetLibrary assets) {
        var texture = assets.LoadTexture(
            "../../../Data/Textures/raindrop.png",
            TextureColorSpace.Srgb,
            new Brightness { Amount = 0.5f });
        
        var material = rendering.CreateMaterial(
            new ParticleMaterial {
                Response = 1,
                Fade = 1f,
                Billboard = BillboardAlign.Velocity
            },
            texture);

        return world.Spawn(new ParticleEmitter {
            Prewarm = 10,
            Rate = 1000,
            Emission = EmitShape.Circle(20),
            Spread = 2,
            Lifetime = 2.0f,
            Capacity = 32000,
            Speed = 50,
            Material = material,
            Blend = BlendMode.Alpha,
            StartSize = new Vector2(0.02f, 1)
        }).At(Vector3d.Zero);
    }
}
