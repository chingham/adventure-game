using System.Text.Json;
using AdventureGame.Systems;
using Quark.Ecs;
using Quark.Kit.Rendering;
using Quark.Numerics;

namespace AdventureGame.Library;

enum LevelReadResult {
    Loaded,
    Locked,    // being written right now, worth retrying
    Invalid
}

// Reads a level from JSON and replays it through the kit. Nothing reaches the world until the whole
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

    public static LevelReadResult TryRead(string path, out LevelDocument level) {
        level = new LevelDocument();

        string text;
        try {
            text = File.ReadAllText(path);
        }
        catch (FileNotFoundException) {
            return Warn($"'{path}' not found.");
        }
        catch (DirectoryNotFoundException) {
            return Warn($"'{path}' not found.");
        }
        catch (IOException) {
            return LevelReadResult.Locked;
        }

        LevelDocument? read;
        try {
            read = JsonSerializer.Deserialize<LevelDocument>(text, Options);
        }
        catch (JsonException e) {
            return Warn(e.Message);
        }

        if (read is null)
            return Warn("the file holds no level.");
        if (read.Validate() is { } error)
            return Warn(error);

        var named = new HashSet<string>();
        for (var i = 0; i < read.Objects.Count; i++) {
            var o = read.Objects[i];
            if (o.Validate() is { } issue)
                return Warn($"object {i} '{o.Type}': {issue}");
            if (o.Id is { } id && !named.Add(id))
                return Warn($"object {i} '{o.Type}': id '{id}' is already taken.");
        }

        // Sequences resolve against those names: a typo that quietly does nothing is the worst kind
        var listening = new HashSet<string>();
        foreach (var sequence in read.Sequences) {
            if (sequence.Validate() is { } issue)
                return Warn($"sequence '{sequence.On}': {issue}.");
            if (!listening.Add(sequence.On!))
                return Warn($"sequence '{sequence.On}': two sequences answer the same signal.");

            foreach (var reference in sequence.References())
                if (!named.Contains(reference))
                    return Warn($"sequence '{sequence.On}': nothing here is called '{reference}'.");
        }

        level = read;
        return LevelReadResult.Loaded;
    }

    static LevelReadResult Warn(string message) {
        Console.Error.WriteLine($"[Level] {message} Keeping the level currently loaded.");
        return LevelReadResult.Invalid;
    }

    // Building

    public static void Apply(LevelDocument level, Bricks kit, GreyboxMaterials materials) {
        foreach (var o in level.Objects)
            Build(o, kit, materials.ByName(o.M));
        foreach (var sequence in level.Sequences)
            kit.Sequence(sequence.On!, sequence.Steps);
    }

    static void Build(ObjectEntry o, Bricks kit, MaterialHandle m) {
        // Verbs that spread over several entities carry no modifiers, so they hand none back
        switch (o.Kind) {
            case "stairs":
                kit.Stairs(o.X, o.Y, Facing(o.Facing), o.Steps, o.W, o.Z, m);
                return;
            case "portal":
                kit.Portal(o.W, o.H, Anchor(o.A!), Anchor(o.B!));
                return;
        }

        var entity = o.Kind switch {
            "block" => kit.Block(o.X, o.Y, o.W, o.D, o.H, o.Z, m, o.Shell),
            "pillar" => kit.Pillar(o.X, o.Y, o.H, o.R, o.Z, m, o.Shell),
            "ramp" => kit.Ramp(o.X, o.Y, Facing(o.Facing), o.W, o.Run, o.H, o.Z, m, o.Shell),
            "point" => kit.Point(o.X, o.Y, o.Z),
            _ => Entity.Null
        };

        if (o.Id is { } id)
            kit.Name(entity, id);
        if (o.Trigger is { } t)
            kit.Trigger(entity, t.Enter, t.Exit, t.Rearm);
        if (o.Interactable is { } i)
            kit.Interactable(entity, o.InteractionOffset, i.Prompt, i.Emit!, i.Reach, i.Once);
        if (o.Space is { } s)
            kit.Space(entity, o.Min, o.Max, s.Distance, s.Fov, s.Pivot == "center", s.Fog);
        if (o.Mover is { } v)
            kit.Mover(entity, o.Origin, o.MoverStop, LevelSpelling.AsMode(v.Mode)!.Value, v.Speed, v.Dwell, v.On);
    }

    static Facing Facing(string? facing) => LevelSpelling.AsFacing(facing)!.Value;

    static Bricks.PortalAnchor Anchor(PortalEndEntry end) =>
        new(end.X, end.Y, end.Z, Facing(end.Facing));
}
