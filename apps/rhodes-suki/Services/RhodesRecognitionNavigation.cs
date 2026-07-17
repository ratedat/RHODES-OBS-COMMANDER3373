using System.Text.Json;
using MaaFramework.Binding;

namespace RhodesSuki.Services;

public sealed record RhodesRecognitionNavigationStep(
    string Type,
    string Label,
    int X,
    int Y,
    int Width,
    int Height,
    int DurationMs);

public sealed record RhodesRecognitionNavigationPlan(
    string ProfileId,
    IReadOnlyList<RhodesRecognitionNavigationStep> OpenSteps,
    IReadOnlyList<RhodesRecognitionNavigationStep> RestoreSteps);

public sealed record RhodesRecognitionNavigationResult(
    bool Succeeded,
    string Detail,
    int CompletedSteps);

public static class RhodesRecognitionNavigation
{
    public static RhodesRecognitionNavigationPlan LoadDefault(string profileId)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "recognition", "scan-profiles.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("scan-profiles.jsonが見つかりません。", path);
        return LoadFromJson(File.ReadAllText(path), profileId);
    }

    public static RhodesRecognitionNavigationPlan LoadFromJson(string json, string profileId)
    {
        using var document = JsonDocument.Parse(json);
        var profiles = document.RootElement.GetProperty("profiles");
        var profile = profiles.EnumerateArray().FirstOrDefault(item =>
            string.Equals(JsonString(item, "id"), profileId, StringComparison.Ordinal));
        if (profile.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"認識プロファイルが見つかりません: {profileId}");

        return new RhodesRecognitionNavigationPlan(
            profileId,
            ParseSteps(profile, "openSteps"),
            ParseSteps(profile, "restoreSteps"));
    }

    public static (int X, int Y) RandomTapPoint(
        RhodesRecognitionNavigationStep step,
        Random? random = null)
    {
        if (!string.Equals(step.Type, "tap", StringComparison.Ordinal))
            throw new ArgumentException("tapステップではありません。", nameof(step));

        random ??= Random.Shared;
        var width = Math.Max(1, step.Width);
        var height = Math.Max(1, step.Height);
        return (
            random.Next(step.X, checked(step.X + width)),
            random.Next(step.Y, checked(step.Y + height)));
    }

    public static async Task<RhodesRecognitionNavigationResult> ExecuteAsync(
        IEnumerable<RhodesRecognitionNavigationStep> steps,
        Func<int, int, CancellationToken, Task<MaaJobStatus>> tapAsync,
        Func<int, CancellationToken, Task>? waitAsync = null,
        CancellationToken cancellationToken = default)
    {
        waitAsync ??= static (durationMs, token) => Task.Delay(durationMs, token);
        var completed = 0;
        foreach (var step in steps)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(step.Type, "wait", StringComparison.Ordinal))
            {
                await waitAsync(Math.Max(0, step.DurationMs), cancellationToken);
                completed++;
                continue;
            }

            if (!string.Equals(step.Type, "tap", StringComparison.Ordinal))
            {
                return new RhodesRecognitionNavigationResult(
                    false,
                    $"禁止または未対応の画面操作です: {step.Type}",
                    completed);
            }

            var point = RandomTapPoint(step);
            var status = await tapAsync(point.X, point.Y, cancellationToken);
            if (status != MaaJobStatus.Succeeded)
            {
                return new RhodesRecognitionNavigationResult(
                    false,
                    $"{step.Label}: tap({point.X},{point.Y}) {status}",
                    completed);
            }
            completed++;
        }

        return new RhodesRecognitionNavigationResult(true, $"画面操作完了: {completed} step", completed);
    }

    private static IReadOnlyList<RhodesRecognitionNavigationStep> ParseSteps(JsonElement profile, string propertyName)
    {
        if (!profile.TryGetProperty(propertyName, out var steps) || steps.ValueKind != JsonValueKind.Array)
            return [];

        return steps.EnumerateArray()
            .Where(step => step.ValueKind == JsonValueKind.Object)
            .Select(ParseStep)
            .ToArray();
    }

    private static RhodesRecognitionNavigationStep ParseStep(JsonElement step)
    {
        var type = JsonString(step, "type").Trim().ToLowerInvariant();
        var label = JsonString(step, "label");
        var durationMs = JsonInt(step, "durationMs");
        if (!string.Equals(type, "tap", StringComparison.Ordinal))
            return new RhodesRecognitionNavigationStep(type, label, 0, 0, 0, 0, durationMs);

        var bounds = step.TryGetProperty("area", out var area) && area.ValueKind == JsonValueKind.Object
            ? area
            : step.TryGetProperty("point", out var point) && point.ValueKind == JsonValueKind.Object
                ? point
                : default;
        if (bounds.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException($"tapステップにarea/pointがありません: {label}");

        return new RhodesRecognitionNavigationStep(
            type,
            label,
            JsonInt(bounds, "x"),
            JsonInt(bounds, "y"),
            Math.Max(1, JsonInt(bounds, "width", 1)),
            Math.Max(1, JsonInt(bounds, "height", 1)),
            durationMs);
    }

    private static string JsonString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

    private static int JsonInt(JsonElement element, string propertyName, int fallback = 0) =>
        element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value)
            ? value
            : fallback;
}
