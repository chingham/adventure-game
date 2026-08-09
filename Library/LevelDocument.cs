using AdventureGame.Systems;
using Quark.Numerics;

namespace AdventureGame.Library;

// The level file: where the player starts, and everything the world is made of.
sealed record LevelDocument {
    public float[]? Spawn { get; init; }
    public IReadOnlyList<ObjectEntry> Objects { get; init; } = [];
    public IReadOnlyList<SequenceEntry> Sequences { get; init; } = [];

    // Read once at startup: moving the start point should not teleport a player mid-session.
    public Vector3d? SpawnPoint => Spawn is { Length: 3 } s ? new Vector3d(s[0], s[1], s[2]) : null;

    public string? Validate() =>
        Spawn is null or { Length: 3 } ? null : "spawn must be [x, y, z]";
}

// One object: a shape verb placed by the minimum corner of its footprint, plus the modifiers riding on
// the entity it spawns. The fields are shared across verbs; each reads the subset it needs.
sealed record ObjectEntry {
    public string Type { get; init; } = "";
    public string? Id { get; init; }
    public string? M { get; init; }
    public bool? Visible { get; init; }
    public bool? Solid { get; init; }

    // Minimum corner of the footprint, and the altitude it sits on
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    // Extent: w/d/h for a block, w+run+h for a ramp, w+steps for stairs, r+h for a pillar
    public float W { get; init; }
    public float D { get; init; }
    public float H { get; init; }
    public float Run { get; init; }
    public int Steps { get; init; }
    public float R { get; init; } = 0.5f;
    public string? Facing { get; init; }

    // Portal ends
    public PortalEndEntry? A { get; init; }
    public PortalEndEntry? B { get; init; }

    // Modifiers
    public TriggerEntry? Trigger { get; init; }
    public InteractableEntry? Interactable { get; init; }
    public SpaceEntry? Space { get; init; }
    public MoverEntry? Mover { get; init; }

    // Reads

    public string Kind => Type.ToLowerInvariant();

    // Stairs and portals spread over several entities, so there is no single one to ride.
    public bool Spreads => Kind is "stairs" or "portal";

    // A volume modifier turns its geometry into air: a space is never solid, a trigger never blocks.
    // Both stop drawing too, unless `visible` asks for them back while they are being placed.
    bool IsAir => Trigger is not null || Space is not null;

    // What carries the body, in order: a modifier that owns one, else `solid`, else a plain solid.
    public Shell Shell => new(
        (Visible ?? !IsAir) ? Look.Visible : Look.Hidden,
        Trigger is not null ? Solidity.Trigger
        : Mover is not null ? Solidity.Moving
        : IsAir ? Solidity.None
        : Solid ?? true ? Solidity.Solid : Solidity.None);

    public Vector3d Min => new(X, Y, Z);
    public Vector3d Max => new(X + W, Y + D, Z + H);

    // Where the shape verb parks this object's origin, and where a mover carries it.
    public Vector3d Origin => Min + Half;
    public Vector3d MoverStop =>
        new Vector3d(Mover!.To![0], Mover.To[1], Mover.To[2]) + Half;

    // Half the object's own extent, which is where its origin sits above the footprint corner.
    Vector3d Half => Kind switch {
        "pillar" => new Vector3d(R, R, H / 2),
        "ramp" => new Vector3d(W / 2, Run / 2, H / 2),
        "point" => Vector3d.Zero,
        _ => new Vector3d(W / 2, D / 2, H / 2)
    };

    // The interaction point, carried from the object's own minimum corner - the convention everywhere
    // in this file - to an offset from its origin, which is what the entity understands.
    public Vector3d InteractionOffset =>
        Interactable?.Offset is { Length: 3 } o ? new Vector3d(o[0], o[1], o[2]) - Half : Vector3d.Zero;

    // Validation

    public string? Validate() => Shape() ?? Modifiers();

    string? Shape() => Kind switch {
        "block" => this is { W: > 0, D: > 0, H: > 0 } ? null : "w, d and h must be positive",
        "point" => null,
        "pillar" => this is { R: > 0, H: > 0 } ? null : "r and h must be positive",
        "ramp" => LevelSpelling.AsFacing(Facing) is null ? $"unknown facing '{Facing}'"
            : this is { W: > 0, Run: > 0, H: > 0 } ? null : "w, run and h must be positive",
        "stairs" => LevelSpelling.AsFacing(Facing) is null ? $"unknown facing '{Facing}'"
            : this is { W: > 0, Steps: > 0 } ? null : "w and steps must be positive",
        "portal" => this is not { W: > 0, H: > 0 } ? "w and h must be positive"
            : A is null || B is null ? "both ends a and b are required"
            : LevelSpelling.AsFacing(A.Facing) is null || LevelSpelling.AsFacing(B.Facing) is null
                ? "each end needs a valid facing" : null,
        _ => "unknown object type"
    };

    bool HasModifiers => Trigger is not null || Interactable is not null
        || Space is not null || Mover is not null;

    string? Modifiers() {
        if (Spreads && HasModifiers)
            return $"a {Kind} spans several entities and cannot carry modifiers";
        if (Space is not null && Kind is "point")
            return "a space needs a volume, not a point";
        // Say so rather than quietly picking a winner
        if (Solid is not null && (Trigger is not null || Space is not null || Mover is not null))
            return "solid contradicts a modifier that already decides this object's body";

        return Trigger?.Validate() ?? Interactable?.Validate() ?? Space?.Validate() ?? Mover?.Validate();
    }
}

// A rule with no place in the world: hearing `on` runs these steps. The step verbs are described
// where they run, in Sequence.cs.
sealed record SequenceEntry {
    public string? On { get; init; }
    public Step[] Steps { get; init; } = [];

    public string? Validate() {
        if (string.IsNullOrEmpty(On))
            return "a sequence needs on";
        if (Steps.Length == 0)
            return "a sequence needs steps";

        foreach (var step in Steps)
            if (step.Validate() is { } error)
                return error;
        return null;
    }

    public IEnumerable<string> References() {
        foreach (var step in Steps)
            foreach (var reference in step.References())
                yield return reference;
    }
}

// One portal end: door footprint corner and the direction faced on arrival.
sealed record PortalEndEntry {
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public string? Facing { get; init; }
}

// Overlap volume: the signals it sends as the character comes and goes. `Rearm` names the signal that
// unlatches it after an entry, so a one-shot volume can wait for whatever it summoned to come home.
sealed record TriggerEntry {
    public string? Enter { get; init; }
    public string? Exit { get; init; }
    public string? Rearm { get; init; }

    public string? Validate() =>
        Enter is null && Exit is null ? "a trigger needs enter or exit" : null;
}

// A point on the object the character can act on. The offset is measured from the object's own minimum
// corner, like every placement here; left out, the point lands in the middle of the object.
sealed record InteractableEntry {
    public float[]? Offset { get; init; }
    public string Prompt { get; init; } = "Use";
    public string? Emit { get; init; }
    public float Reach { get; init; } = 1.8f;
    public bool Once { get; init; }

    public string? Validate() =>
        string.IsNullOrEmpty(Emit) ? "an interactable needs emit"
        : Offset is not (null or { Length: 3 }) ? "offset must be [x, y, z]"
        : Reach > 0 ? null : "reach must be positive";
}

// A round trip between where the object stands and one far stop, given as a footprint corner like
// every placement here. A shuttle leaves on its own; a called one waits for `on`.
sealed record MoverEntry {
    public float[]? To { get; init; }
    public string? Mode { get; init; }
    public string? On { get; init; }
    public float Speed { get; init; } = 6;
    public float Dwell { get; init; } = 1;

    public string? Validate() =>
        To is not { Length: 3 } ? "a mover needs to [x, y, z]"
        : LevelSpelling.AsMode(Mode) is not { } mode ? $"unknown mode '{Mode}'"
        : Speed <= 0 ? "speed must be positive"
        : Dwell <= 0 ? "dwell must be positive"
        : mode is PlatformMode.Called && string.IsNullOrEmpty(On) ? "a called mover needs on"
        : null;
}

// How this volume frames the place: where the isometric shot sits, how wide it is, what it holds on,
// and its fog.
sealed record SpaceEntry {
    public float Distance { get; init; } = 90;
    public float Fov { get; init; } = 0.4f;
    public string? Pivot { get; init; }
    public float? Fog { get; init; }

    public string? Validate() =>
        this is not { Distance: > 0, Fov: > 0 } ? "distance and fov must be positive"
        : Pivot is not (null or "follow" or "center") ? $"unknown pivot '{Pivot}'" : null;
}

// Spellings the file uses for engine enums, resolved in one place so validation and building agree.
static class LevelSpelling {
    public static Facing? AsFacing(string? s) =>
        Enum.TryParse<Facing>(s, ignoreCase: true, out var parsed) ? parsed : null;

    public static PlatformMode? AsMode(string? s) =>
        Enum.TryParse<PlatformMode>(s, ignoreCase: true, out var parsed) ? parsed : null;
}
