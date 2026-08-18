using AdventureGame.App;
using Quark.Graphics;
using Quark.Kit.Rendering;
using WebGpuSharp;

namespace AdventureGame.Presentation.Highlight;

static class HighlightPasses {
    public static void Install(DefaultRenderingModule rendering) {
        var mask = rendering.CreateMaterial(new StencilMaskMaterial());
        var view = rendering.MainView;
        
        // Stencil format:
        // Bit 0: Character visible
        // Bit 1: Character occluded
        // Bit 2: Highlight objects
        
        // Character: Write bit 0 where it's visible (depth test)
        var visible = view.AddPass(new ScenePass {
            Label = "Character visible",
            Layers = Layers.Render.Character,
            Material = mask,
            State = new RenderState {
                DepthCompare = CompareFunction.LessEqual,
                DepthWrite = false,
                Stencil = Stencil.Mark(0b001) with { WriteMask = 0b001 }
            }
        }, after: view.Passes.Transparent);
        
        // Character: Write bit 1 where it's occluded (bit 0 not set)
        view.AddPass(new ScenePass {
            Label = "Character occluded",
            Layers = Layers.Render.Character,
            Material = mask,
            State = RenderState.Overlay with {
                Stencil = new Stencil {
                    Compare = CompareFunction.NotEqual,
                    Reference = 0b011,
                    ReadMask = 0b001,
                    WriteMask = 0b10,
                    Pass = StencilOperation.Replace
                }
            }
        }, after: visible);
        
        // Highlight: Write bit 2 everywhere, without depth test
        view.AddPass(new ScenePass {
            Label = "Highlight",
            Layers = Layers.Render.Highlight,
            Material = mask,
            State = RenderState.Overlay with {
                Stencil = Stencil.Mark(0b100) with { WriteMask = 0b100 }
            }
        }, after: visible);
    }
}