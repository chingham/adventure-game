# Contour + ombre à travers les murs — tuto d'implémentation

Le moteur sait maintenant lire le stencil depuis un post effect (`KitTargets.Stencil`).
Tout le reste se fait côté jeu, sans texture annexe et sans re-rendu.

## Le principe en une phrase

Trois passes ne peignent **rien** : elles ne font que poser des bits dans le stencil.
Un seul post effect lit ces bits et dessine l'ombre et les contours.

| Bit | Valeur | Sens |
|---|---|---|
| 0 | 1 | perso visible (marqueur de travail, jamais lu par l'effet) |
| 1 | 2 | **perso caché** → ombre + contour |
| 2 | 4 | **highlight** → contour seul |

Le bit 0 existe uniquement pour que le bit 1 puisse dire « couvert par le perso mais
pas visible ». C'est le fix anti-auto-silhouette : sans lui, les bottes cachées
derrière les jambes compteraient comme cachées par un mur.

---

## 1. Activer le stencil

`Program.cs` :

```csharp
.UseDefaultRendering(rendering => {
    rendering.Stencil = true;
})
```

Sans ça, l'effet lève une exception explicite au démarrage.

---

## 2. Les layers de rendu

`App/Layers.cs` porte aujourd'hui les layers physiques. Ajoute une section :

```csharp
// Render layers: qui participe aux passes de stencil
static class RenderMasks {
    public static readonly RenderLayers Character = RenderLayers.Bit(1);
    public static readonly RenderLayers Highlight = RenderLayers.Bit(2);
}
```

Un mesh garde toujours `RenderLayers.Default` en plus, sinon il disparaît du rendu
normal — c'est un bit qui s'ajoute, pas qui remplace.

---

## 3. Taguer le personnage

Dans `Character.Spawn`, les trois meshes sont des enfants (corps, yeux, poêle).
Chacun a besoin du bit :

```csharp
Layers = RenderLayers.Default | RenderMasks.Character
```

À poser sur le `RenderMesh` de chacun. Selon ce que renvoient `primitives.Capsule`
et `primitives.Box`, ça se fait soit à la construction du bundle, soit en éditant le
`RenderMesh` juste après le spawn.

---

## 4. Le matériau de marquage

Un seul matériau, pour les trois passes. Il n'écrit aucune couleur — il n'est là que
pour le stencil qu'il laisse.

`Presentation/Highlight/StencilMaskMaterial.cs` :

```csharp
[Material(MaterialTemplate.Unlit)]
struct StencilMaskMaterial : IMaterial {
    // Rien à peindre: la passe ne sert qu'au stencil.
    public static RenderState State => new() { ColorMask = ColorWriteMask.None };

    public static string Surface => """
        fn surface(surf: SurfaceInput, m: Material) -> Surface {
            return surface_default();
        }
        """;
}
```

`ColorMask` appartient au matériau, pas à la passe — une `ScenePass` impose la
profondeur et le stencil, jamais la façon dont la couleur atterrit.

---

## 5. Les trois passes

`Presentation/Highlight/HighlightPasses.cs`, appelé au `Provide` de la feature.
L'ordre compte : le perso d'abord (il utilise le bit 0 comme brouillon), le
highlight ensuite.

```csharp
var mask = rendering.CreateMaterial(new StencilMaskMaterial());
var view = rendering.MainView;

// 1. Le perso là où il est visible → bit 0.
//    LessEqual: sa face la plus proche a écrit cette profondeur dans la passe
//    opaque, donc seule elle passe; tout ce qui est derrière un mur échoue.
var visible = view.AddPass(new ScenePass {
    Label = "Character visible",
    Layers = RenderMasks.Character,
    Material = mask.Handle,
    State = new RenderState {
        DepthCompare = CompareFunction.LessEqual,
        DepthWrite = false,
        Cull = CullMode.Back,
        Stencil = Stencil.Mark(1) with { WriteMask = 1 }
    }
}, after: view.Passes.Transparent);

// 2. Le perso là où le bit 0 est absent → bit 1. Aucune profondeur testée, donc
//    l'auto-occlusion ne peut pas revenir. Reference 3 pour que le test lise le
//    bit 0 et que l'écriture pose le bit 1.
view.AddPass(new ScenePass {
    Label = "Character hidden",
    Layers = RenderMasks.Character,
    Material = mask.Handle,
    State = RenderState.Overlay with {
        Cull = CullMode.Back,
        Stencil = new Stencil {
            Compare = CompareFunction.NotEqual,
            Reference = 3,
            ReadMask = 1,
            WriteMask = 2,
            Pass = StencilOperation.Replace
        }
    }
}, after: visible);

// 3. L'objet surligné → bit 2, partout, sans condition.
view.AddPass(new ScenePass {
    Label = "Highlight",
    Layers = RenderMasks.Highlight,
    Material = mask.Handle,
    State = RenderState.Overlay with {
        Cull = CullMode.Back,
        Stencil = Stencil.Mark(4) with { WriteMask = 4 }
    }
}, after: visible);
```

---

## 6. L'effet

`Presentation/Highlight/HighlightEffect.cs`. En `Ldr` (après le tonemap), pour que
les couleurs soient celles que tu écris et pas ce que le tonemap en fait.

```csharp
struct HighlightEffect : IPostEffect {
    public Vector4 Shadow;    // l'aplat du perso caché (alpha = force)
    public Vector4 Outline;   // le liseré, pour les deux
    public float Thickness;   // en pixels

    public static string Effect => """
        const CHARACTER: u32 = 2u;
        const HIGHLIGHT: u32 = 4u;

        // Le bord: ce que le voisinage porte moins ce que le centre porte.
        fn ring(uv: vec2f, mask: u32, step: vec2f) -> f32 {
            let inside = stencil_coverage(uv, mask);
            let around = max(
                max(stencil_coverage(uv + vec2f(step.x, 0.0), mask),
                    stencil_coverage(uv - vec2f(step.x, 0.0), mask)),
                max(stencil_coverage(uv + vec2f(0.0, step.y), mask),
                    stencil_coverage(uv - vec2f(0.0, step.y), mask)));
            return saturate(around - inside);
        }

        fn post(color: vec3f, uv: vec2f, p: Params) -> vec3f {
            let step = texel_size() * p.thickness;

            var c = color;
            c = mix(c, p.shadow.rgb, stencil_coverage(uv, CHARACTER) * p.shadow.a);
            c = mix(c, p.outline.rgb, ring(uv, CHARACTER, step) * p.outline.a);
            c = mix(c, p.outline.rgb, ring(uv, HIGHLIGHT, step) * p.outline.a);
            return c;
        }
        """;

    public static IReadOnlyList<string> Reads => [KitTargets.Stencil];
}
```

Enregistrement dans `PresentationFeature.Provide` :

```csharp
game.Provide(game.Rendering.AddPostEffect(
    new HighlightEffect {
        Shadow = ...,
        Outline = ...,
        Thickness = 2f
    },
    PostEffectSpace.Ldr));
```

`stencil_coverage` renvoie la fraction des samples MSAA qui portent le bit, donc les
bords sortent antialiasés sans rien faire de plus.

---

## 7. Brancher la sélection

`InteractionSystem` connaît déjà le candidat (`Interactor.Candidate`). Il suffit de
poser et retirer le bit `Highlight` sur son `RenderMesh` quand le candidat change —
en gardant le masque d'origine pour le restaurer, l'entité venant du fichier de
niveau.

Un interactable sans `RenderMesh` (un hotspot invisible) n'a rien à contourer : à
ignorer, le cercle et le glyph de l'UI suffiront pour ceux-là.

Et `Debug/InteractionProbe.cs` peut sauter, son propre commentaire le demande.

---

## Réglage

- **Aplat opaque** (`Shadow.a` proche de 1) plutôt que translucide : sinon le perso
  disparaît devant un objet sombre. C'est le piège de la demo.
- Module la force de l'ombre sur `CameraDirector.Target` (0 = suivi, 1 = iso) si tu
  ne la veux qu'en vue isométrique.
