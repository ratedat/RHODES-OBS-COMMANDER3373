using System.Text.Json;

namespace RhodesSuki.Services;

public sealed record RhodesRecognitionSwipeArea(int X, int Y, int Width, int Height);

public sealed record RhodesRecognitionScrollPass(
    string Axis,
    string Direction,
    string Label,
    RhodesRecognitionSwipeArea StartArea,
    RhodesRecognitionSwipeArea EndArea,
    int DurationMs,
    int MaxScrolls,
    int MinScrolls,
    int EndFingerprintStableCount,
    int CandidateStableEndCount,
    int CaptureDelayMs,
    bool CollectCandidates,
    bool MirrorPreviousPassScrolls);

public sealed record RhodesRecognitionSwipePoint(int StartX, int StartY, int EndX, int EndY);

public static class RhodesRecognitionScrollPlan
{
    public static RhodesRecognitionSwipeArea LoadScanRegionDefault(string profileId)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "recognition", "scan-profiles.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("scan-profiles.jsonが見つかりません。", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var profile = FindProfile(document.RootElement, profileId);
        return profile.TryGetProperty("scanRegion", out var area) && area.ValueKind == JsonValueKind.Object
            ? new RhodesRecognitionSwipeArea(
                JsonInt(area, "x"),
                JsonInt(area, "y"),
                Math.Max(1, JsonInt(area, "width", 1280)),
                Math.Max(1, JsonInt(area, "height", 720)))
            : new RhodesRecognitionSwipeArea(0, 0, 1280, 720);
    }

    public static IReadOnlyList<RhodesRecognitionScrollPass> LoadDefault(string profileId)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "recognition", "scan-profiles.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("scan-profiles.jsonが見つかりません。", path);
        return LoadFromJson(File.ReadAllText(path), profileId);
    }

    public static IReadOnlyList<RhodesRecognitionScrollPass> LoadFromJson(string json, string profileId)
    {
        using var document = JsonDocument.Parse(json);
        var profile = FindProfile(document.RootElement, profileId);
        if (!profile.TryGetProperty("scrollPasses", out var passes) || passes.ValueKind != JsonValueKind.Array)
            return [];

        return passes.EnumerateArray()
            .Where(pass => pass.ValueKind == JsonValueKind.Object)
            .Select(ParsePass)
            .ToArray();
    }

    private static JsonElement FindProfile(JsonElement root, string profileId)
    {
        var profile = root.GetProperty("profiles").EnumerateArray().FirstOrDefault(item =>
            string.Equals(JsonString(item, "id"), profileId, StringComparison.Ordinal));
        if (profile.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"認識プロファイルが見つかりません: {profileId}");
        return profile;
    }

    public static RhodesRecognitionSwipePoint RandomSwipe(
        RhodesRecognitionScrollPass pass,
        Random? random = null)
    {
        random ??= Random.Shared;
        var start = RandomPoint(pass.StartArea, random);
        var end = RandomPoint(pass.EndArea, random);
        return new RhodesRecognitionSwipePoint(start.X, start.Y, end.X, end.Y);
    }

    private static RhodesRecognitionScrollPass ParsePass(JsonElement pass)
    {
        if (!pass.TryGetProperty("scroll", out var scroll) || scroll.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"scrollPassesにscrollがありません: {JsonString(pass, "label")}");

        var startArea = ParseArea(scroll, "startArea", "start");
        var endArea = ParseArea(scroll, "endArea", "end");
        return new RhodesRecognitionScrollPass(
            JsonString(pass, "axis"),
            JsonString(pass, "direction"),
            JsonString(pass, "label"),
            startArea,
            endArea,
            Math.Max(1, JsonInt(scroll, "durationMs", 500)),
            Math.Max(0, JsonInt(pass, "maxScrolls")),
            Math.Max(0, JsonInt(pass, "minScrolls")),
            Math.Max(1, JsonInt(pass, "endFingerprintStableCount", 1)),
            Math.Max(0, JsonInt(pass, "candidateStableEndCount")),
            Math.Max(0, JsonInt(pass, "captureDelayMs")),
            JsonBool(pass, "collectCandidates", true),
            JsonBool(pass, "mirrorPreviousPassScrolls", false));
    }

    private static RhodesRecognitionSwipeArea ParseArea(JsonElement scroll, string areaName, string pointName)
    {
        if (scroll.TryGetProperty(areaName, out var area) && area.ValueKind == JsonValueKind.Object)
        {
            return new RhodesRecognitionSwipeArea(
                JsonInt(area, "x"),
                JsonInt(area, "y"),
                Math.Max(1, JsonInt(area, "width", 1)),
                Math.Max(1, JsonInt(area, "height", 1)));
        }
        if (scroll.TryGetProperty(pointName, out var point) && point.ValueKind == JsonValueKind.Object)
            return new RhodesRecognitionSwipeArea(JsonInt(point, "x"), JsonInt(point, "y"), 1, 1);
        throw new InvalidOperationException($"scrollに{areaName}/{pointName}がありません。");
    }

    private static (int X, int Y) RandomPoint(RhodesRecognitionSwipeArea area, Random random) =>
        (random.Next(area.X, checked(area.X + Math.Max(1, area.Width))),
         random.Next(area.Y, checked(area.Y + Math.Max(1, area.Height))));

    private static string JsonString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

    private static int JsonInt(JsonElement element, string propertyName, int fallback = 0) =>
        element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : fallback;

    private static bool JsonBool(JsonElement element, string propertyName, bool fallback) =>
        element.TryGetProperty(propertyName, out var property)
            ? property.ValueKind == JsonValueKind.True
            : fallback;
}
