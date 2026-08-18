using Quark.Kit;

namespace AdventureGame.App;

static class Layers {
    public static class Physics {
        public static readonly LayerMask Environment = 2;
        public static readonly LayerMask Character = 4;
        public static readonly LayerMask Trigger = 8;
    }

    public static class Render {
        public static readonly LayerMask Character = 2;
        public static readonly LayerMask Highlight = 4;
    }
}