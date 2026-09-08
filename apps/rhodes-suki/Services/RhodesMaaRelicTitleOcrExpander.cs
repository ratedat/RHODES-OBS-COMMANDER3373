using System.Text.Json;
using System.Text.Json.Nodes;
using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

public sealed record MaaRelicTitleRefinement(MaaTaskRunResult Frame, IReadOnlyList<MaaTaskRunResult> Attempts);

public static class RhodesMaaRelicTitleOcrExpander
{
    public const string EntryPrefix = "relic.card.name.cyan.";
    public const int MaximumRetriesPerFrame = 4;

    public static IReadOnlyList<MaaDynamicOcrRequest> BuildRequests(MaaTaskRunResult frame, MaaOwnedImage image, string campaignId) =>
        CreatePlan(frame, image, campaignId)?.Targets.Select(target => target.Request).ToArray() ?? [];

    public static async Task<MaaRelicTitleRefinement> RefineAsync(
        MaaTaskRunResult frame, MaaOwnedImage image, string campaignId,
        Func<MaaDynamicOcrRequest, CancellationToken, Task<MaaTaskRunResult>> recognize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plan = CreatePlan(frame, image, campaignId);
        if (plan is null || plan.Targets.Count == 0) return new MaaRelicTitleRefinement(frame, []);
        var attempts = new List<MaaTaskRunResult>();
        var changed = false;
        foreach (var target in plan.Targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await recognize(target.Request, cancellationToken);
            attempts.Add(result);
            if (!result.Succeeded || !result.Hit || result.Entry != target.Request.Entry) continue;
            var rows = Rows(Parse(result.RecognitionDetailJson));
            if (rows.Count != 1 || Score(rows[0]) < 0.90
                || RhodesOcrNameCorrections.Normalize(Text(rows[0])) != target.NormalizedName) continue;
            target.Row["rhodes_name_retry"] = new JsonObject
            {
                ["original_text"] = Text(target.Row), ["original_score"] = Score(target.Row), ["entry"] = result.Entry,
            };
            target.Row["text"] = target.Name;
            target.Row["score"] = Score(rows[0]);
            changed = true;
        }
        return new MaaRelicTitleRefinement(changed ? frame with { RecognitionDetailJson = plan.Document.ToJsonString() } : frame, attempts);
    }

    internal static bool IsTitleForeground(SKColor color) => color.Green >= 90 && color.Blue >= 90
        && color.Green - color.Red >= 25 && color.Blue - color.Red >= 25
        && Math.Abs(color.Green - color.Blue) <= 80;

    private static Plan? CreatePlan(MaaTaskRunResult frame, MaaOwnedImage image, string campaignId)
    {
        if (!frame.Succeeded || !frame.Hit || frame.Entry != "RhodesOcrRegion_relic_list_text"
            || image.Length == 0 || string.IsNullOrWhiteSpace(campaignId)) return null;
        var document = Parse(frame.RecognitionDetailJson);
        if (document is null) return null;
        var resolved = RhodesMaaLocalCandidateConverter.FromTaskResults("relicsFull", [frame], campaignId)
            .Select(item => item.RelicId).ToHashSet(StringComparer.Ordinal);
        var names = RhodesRecognitionCatalogCache.Load().Relics.Where(item => item.CampaignId == campaignId)
            .Select(item => new { item.Id, item.Name, Key = RhodesOcrNameCorrections.Normalize(item.Name) })
            .Where(item => item.Key.Length >= 4).ToArray();
        var targets = new List<Target>();
        SKBitmap? bitmap = null;
        try
        {
            foreach (var row in Rows(document))
            {
                var text = RhodesOcrNameCorrections.Normalize(Text(row));
                var matches = names.Where(item => IsPossibleTitlePrefix(text, item.Key)).Take(2).ToArray();
                if (matches.Length != 1 || resolved.Contains(matches[0].Id) || Box(row) is not { } box) continue;
                var x = box[0] - 5;
                var y = box[1] - 5;
                var width = box[2] + 10;
                var height = box[3] + 10;
                if (box[0] < 5 || box[1] < 5 || box[2] is <= 0 or > 500 || box[3] is < 10 or > 45) continue;
                bitmap ??= image.CreateBitmap();
                if ((long)x + width > bitmap.Width || (long)y + height > bitmap.Height) continue;
                var foreground = 0;
                for (var py = y; py < y + height; py++)
                    for (var px = x; px < x + width; px++)
                        if (IsTitleForeground(bitmap.GetPixel(px, py))) foreground++;
                var ratio = foreground / (double)(width * height);
                if (foreground < 16 || ratio is < 0.02 or > 0.70) continue;
                targets.Add(new Target(new MaaDynamicOcrRequest($"{EntryPrefix}{targets.Count}", x, y, width, height, 4, Score(row)),
                    row, matches[0].Name, matches[0].Key));
                if (targets.Count >= MaximumRetriesPerFrame) break;
            }
        }
        finally { bitmap?.Dispose(); }
        return new Plan(document, targets);
    }

    private static bool IsPossibleTitlePrefix(string text, string name)
    {
        if (text.Length <= name.Length || text.Length > name.Length + 16) return false;
        if (text.StartsWith(name, StringComparison.Ordinal)) return true;
        if (name.Length < 6) return false;
        // A single substituted title character only schedules OCR; acceptance still
        // requires the complete formal name from the independently filtered image.
        var differences = 0;
        for (var i = 0; i < name.Length; i++)
            if (text[i] != name[i] && ++differences > 1) return false;
        return differences == 1;
    }

    private static JsonObject? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private static IReadOnlyList<JsonObject> Rows(JsonObject? document)
    {
        if (document is null) return [];
        var root = document["result"] as JsonObject ?? document;
        foreach (var key in new[] { "filtered", "filtered_results", "filteredResults" })
            if (root[key] is JsonArray { Count: > 0 } array) return array.OfType<JsonObject>().ToArray();
        foreach (var key in new[] { "best", "best_result", "bestResult" })
            if (root[key] is JsonObject best) return [best];
        foreach (var key in new[] { "all", "all_results", "allResults" })
            if (root[key] is JsonArray { Count: > 0 } array) return array.OfType<JsonObject>().ToArray();
        return [];
    }

    private static string Text(JsonObject row) => row["text"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";
    private static double Score(JsonObject row) => row["score"] is JsonValue value && value.TryGetValue<double>(out var score)
        && double.IsFinite(score) ? Math.Clamp(score, 0, 1) : 0;
    private static int[]? Box(JsonObject row)
    {
        if (row["box"] is not JsonArray { Count: 4 } box) return null;
        var values = new int[4];
        for (var i = 0; i < 4; i++) if (box[i] is not JsonValue value || !value.TryGetValue<int>(out values[i])) return null;
        return values;
    }

    private sealed record Target(MaaDynamicOcrRequest Request, JsonObject Row, string Name, string NormalizedName);
    private sealed record Plan(JsonObject Document, IReadOnlyList<Target> Targets);
}
