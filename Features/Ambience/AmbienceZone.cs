using System.Numerics;

namespace AdventureGame.Features.Ambience;

/// <summary>
/// What a place feels like. It rides a volume like any other aspect, or stands alone as the level's
/// own - the open air. Each part is optional on its own, so a nook that only thickens the fog leaves
/// the colour and the weather to the room around it.
/// </summary>
struct AmbienceZone {
    public Fog? Fog;
    public Grade? Grade;
    public Rain? Rain;
}

// How far you see in here
struct Fog {
    public double Density;
    public Vector4 Color;
}

// What the place does to colour
struct Grade {
    public double Saturation;
    public double Warmth;
}

// How hard it rains here. One number, because the emitter, the loop's gain and the haze all read it:
// three settings could drift apart mid-transition, one cannot.
struct Rain {
    public double Intensity;
    public double Tilt;        // degrees the fall leans, matched by the streaks
}

// Authored, not yet drawn: everything inside goes black, softened over Edge metres. Unlike the aspects
// above this one is not resolved by where the player stands - it is a thing in the world, and whatever
// draws it will have to find it from wherever the camera is.
struct Shroud {
    public double Edge;
    public Vector4 Color;
}
