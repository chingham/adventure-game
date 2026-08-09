using System.Text.Json;
using AdventureGame.Systems;
using Quark.Numerics;

namespace AdventureGame.Library;

enum LevelReadResult {
    Loaded,
    Locked,    // being written right now, worth retrying
    Invalid
}

// One brick in the level file. The fields are shared across kinds; each kind reads the subset it needs.
sealed record BrickEntry {
    public string Brick { get; init; } = "";

    // Minimum corner of the footprint, and the altitude it sits on
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }

    // Dimensions: w/d/h for a block, w+run+h for a ramp, w+steps for stairs, r+h for a pillar
    public float W { get; init; }
    public float D { get; init; }
    public float H { get; init; }
    public float Run { get; init; }
    public int Steps { get; init; }
    public float R { get; init; } = 0.5f;

    public string? Facing { get; init; }
    public string? M { get; init; }

    // Name a zone carries into the world, so events can be read back
    public string? Id { get; init; }

    // Devices: the signal a button or interaction point emits, and the platform route (2 corner stops,
    // home first). An interaction point also reads what its prompt says and whether it retires on use.
    public string? Emit { get; init; }
    public string? Prompt { get; init; }
    public bool Once { get; init; }
    public string? Mode { get; init; }
    public string? On { get; init; }
    public float Speed { get; init; }
    public float Dwell { get; init; }
    public float[][]? Stops { get; init; }

    // Portal ends
    public PortalEndEntry? A { get; init; }
    public PortalEndEntry? B { get; init; }

    // Space mood: how far the isometric shot sits, how wide, what it holds on, and its fog
    public float Distance { get; init; } = 90;
    public float Fov { get; init; } = 0.4f;
    public string? Pivot { get; init; }
    public float? Fog { get; init; }
}

// One portal end in the file: door footprint corner and the direction faced on arrival.
sealed record PortalEndEntry {
    public float X { get; init; }
    public float Y { get; init; }
    public float Z { get; init; }
    public string? Facing { get; init; }
}

// Reads a brick list from JSON and replays it through the kit. Nothing reaches the world until the whole
// file validates, so a half-typed save leaves the running level alone.
static class LevelFile {
    const string RelativePath = "Data/level.json";

    // Data/ is copied next to the exe, but live editing has to watch the file actually being edited:
    // when running out of bin/<config>/<tfm>, walk back up to the project copy and prefer it.
    public static readonly string Path = Resolve();

    static string Resolve() {
        var source = System.IO.Path.GetFullPath(
            System.IO.Path.Combine(AppContext.BaseDirectory, "../../..", RelativePath));
        return File.Exists(source) ? source : RelativePath;
    }

    static readonly JsonSerializerOptions Options = new() {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    // Reading

    public static LevelReadResult TryRead(string path, out IReadOnlyList<BrickEntry> bricks) {
        bricks = [];

        string text;
        try {
            text = File.ReadAllText(path);
        }
        catch (FileNotFoundException) {
            Warn($"'{path}' not found.");
            return LevelReadResult.Invalid;
        }
        catch (DirectoryNotFoundException) {
            Warn($"'{path}' not found.");
            return LevelReadResult.Invalid;
        }
        catch (IOException) {
            return LevelReadResult.Locked;
        }

        List<BrickEntry>? entries;
        try {
            entries = JsonSerializer.Deserialize<List<BrickEntry>>(text, Options);
        }
        catch (JsonException e) {
            Warn(e.Message);
            return LevelReadResult.Invalid;
        }

        if (entries is null) {
            Warn("the file holds no brick list.");
            return LevelReadResult.Invalid;
        }

        for (var i = 0; i < entries.Count; i++)
            if (Validate(entries[i]) is { } error) {
                Warn($"brick {i} '{entries[i].Brick}': {error}");
                return LevelReadResult.Invalid;
            }

        bricks = entries;
        return LevelReadResult.Loaded;
    }

    static string? Validate(BrickEntry b) => b.Brick.ToLowerInvariant() switch {
        "block" or "zone" => b is { W: > 0, D: > 0, H: > 0 } ? null : "w, d and h must be positive",
        "button" => b is { W: > 0, D: > 0 }
            ? string.IsNullOrEmpty(b.Emit) ? "emit is required" : null
            : "w and d must be positive",
        "interact" => string.IsNullOrEmpty(b.Emit) ? "emit is required"
            : b.R > 0 ? null : "r must be positive",
        "space" => b is not { W: > 0, D: > 0, H: > 0 } ? "w, d and h must be positive"
            : b is not { Distance: > 0, Fov: > 0 } ? "distance and fov must be positive"
            : b.Pivot is not (null or "follow" or "center") ? $"unknown pivot '{b.Pivot}'" : null,
        "platform" => ValidatePlatform(b),
        "portal" => b is not { W: > 0, H: > 0 } ? "w and h must be positive"
            : b.A is null || b.B is null ? "both ends a and b are required"
            : ParseFacing(b.A.Facing) is null || ParseFacing(b.B.Facing) is null
                ? "each end needs a valid facing" : null,
        "pillar" => b is { R: > 0, H: > 0 } ? null : "r and h must be positive",
        "ramp" => ParseFacing(b.Facing) is null
            ? $"unknown facing '{b.Facing}'"
            : b is { W: > 0, Run: > 0, H: > 0 } ? null : "w, run and h must be positive",
        "stairs" => ParseFacing(b.Facing) is null
            ? $"unknown facing '{b.Facing}'"
            : b is { W: > 0, Steps: > 0 } ? null : "w and steps must be positive",
        _ => $"unknown brick kind"
    };

    static string? ValidatePlatform(BrickEntry b) {
        if (b is not { W: > 0, D: > 0, H: > 0 })
            return "w, d and h must be positive";
        if (b.Stops is not [{ Length: 3 }, { Length: 3 }])
            return "stops must hold exactly 2 [x, y, z] corners (home first)";

        return ParseMode(b.Mode) switch {
            null => $"unknown mode '{b.Mode}'",
            _ when b.Speed <= 0 => "speed must be positive",
            _ when b.Dwell <= 0 => "dwell must be positive",
            PlatformMode.Called when string.IsNullOrEmpty(b.On) => "on is required",
            _ => null
        };
    }

    static Facing? ParseFacing(string? facing) =>
        Enum.TryParse<Facing>(facing, ignoreCase: true, out var parsed) ? parsed : null;

    static PlatformMode? ParseMode(string? mode) =>
        Enum.TryParse<PlatformMode>(mode, ignoreCase: true, out var parsed) ? parsed : null;

    static Vector3d Corner(float[] stop) => new(stop[0], stop[1], stop[2]);

    static Bricks.PortalAnchor Anchor(PortalEndEntry end) =>
        new(end.X, end.Y, end.Z, ParseFacing(end.Facing)!.Value);

    static void Warn(string message) =>
        Console.Error.WriteLine($"[Level] {message} Keeping the level currently loaded.");

    // Building

    public static void Apply(IReadOnlyList<BrickEntry> bricks, Bricks kit, GreyboxMaterials materials) {
        foreach (var b in bricks) {
            var m = materials.ByName(b.M);
            switch (b.Brick.ToLowerInvariant()) {
                case "block":
                    kit.Block(b.X, b.Y, b.W, b.D, b.H, b.Z, m);
                    break;
                case "zone":
                    kit.Zone(b.X, b.Y, b.W, b.D, b.H, b.Z, b.Id);
                    break;
                case "button":
                    kit.Button(b.X, b.Y, b.W, b.D, b.Z, b.Emit!);
                    break;
                case "interact":
                    kit.Interact(b.X, b.Y, b.W, b.D, b.H, b.Z, b.R, b.Prompt ?? "Use", b.Emit!, b.Once);
                    break;
                case "space":
                    kit.Space(b.X, b.Y, b.Z, b.W, b.D, b.H, b.Distance, b.Fov, b.Pivot == "center", b.Fog, b.Id);
                    break;
                case "portal":
                    kit.Portal(b.W, b.H, Anchor(b.A!), Anchor(b.B!));
                    break;
                case "platform":
                    kit.Platform(b.W, b.D, b.H, Corner(b.Stops![0]), Corner(b.Stops[1]), new MovingPlatform {
                        Mode = ParseMode(b.Mode)!.Value,
                        Speed = b.Speed,
                        Dwell = b.Dwell,
                        On = b.On
                    }, m);
                    break;
                case "pillar":
                    kit.Pillar(b.X, b.Y, b.H, b.R, b.Z, m);
                    break;
                case "ramp":
                    kit.Ramp(b.X, b.Y, ParseFacing(b.Facing)!.Value, b.W, b.Run, b.H, b.Z, m);
                    break;
                case "stairs":
                    kit.Stairs(b.X, b.Y, ParseFacing(b.Facing)!.Value, b.Steps, b.W, b.Z, m);
                    break;
            }
        }
    }
}
