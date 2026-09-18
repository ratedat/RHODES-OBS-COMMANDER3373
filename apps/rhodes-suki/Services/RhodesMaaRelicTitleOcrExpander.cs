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

    public static MaaTaskRunResult MarkTitleOnly(MaaTaskRunResult frame)
    {
        var document = Parse(frame.RecognitionDetailJson);
        if (document is null) return frame;
        document["rhodes_title_only"] = true;
        return frame with { RecognitionDetailJson = document.ToJsonString() };
    }

    public static IReadOnlyList<MaaDynamicOcrRequest> BuildRequests(MaaTaskRunResult frame, MaaOwnedImage image, string campaignId) =>
        CreatePlan(frame, image, campaignId)?.Targets.Select(target => target.Request).ToArray() ?? [];

    public static async Task<MaaRelicTitleRefinement> RefineAsync(
        MaaTaskRunResult frame, MaaOwnedImage image, string campaignId,
        Func<MaaDynamicOcrRequest, CancellationToken, Task<MaaTaskRunResult>> recognize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (campaignId == "is5_sarkaz" && frame.Entry == RhodesMaaRelicTitleImage.ListEntry
            && frame.Succeeded && Parse(frame.RecognitionDetailJson) is { } titleDocument
            && titleDocument["rhodes_title_only"] is JsonValue marker
            && marker.TryGetValue<bool>(out var titleOnly) && titleOnly)
            return await RefineTitleBandsAsync(frame, image, campaignId, titleDocument, recognize, cancellationToken);
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

    private static async Task<MaaRelicTitleRefinement> RefineTitleBandsAsync(
        MaaTaskRunResult frame, MaaOwnedImage image, string campaignId, JsonObject document,
        Func<MaaDynamicOcrRequest, CancellationToken, Task<MaaTaskRunResult>> recognize,
        CancellationToken cancellationToken)
    {
        var root = document["result"] as JsonObject ?? document;
        if (root["filtered"] is not JsonArray rows) return new MaaRelicTitleRefinement(frame, []);
        var resolvedText = RhodesMaaLocalCandidateConverter.FromTaskResults("relicsFull", [frame], campaignId)
            .SelectMany(item => new[] { item.RawText, item.Label }).ToHashSet(StringComparer.Ordinal);
        var names = RhodesRecognitionCatalogCache.Load().Relics.Where(item => item.CampaignId == campaignId)
            .GroupBy(item => TitleKey(item.Name)).Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().Name, StringComparer.Ordinal);
        var attempts = new List<MaaTaskRunResult>();
        var changed = false;
        foreach (var region in RhodesMaaRelicTitleImage.FindTitleRegions(image))
        {
            if (attempts.Count >= MaximumRetriesPerFrame) break;
            cancellationToken.ThrowIfCancellationRequested();
            var fragments = rows.OfType<JsonObject>().Where(row => Box(row) is { } box
                && box[0] >= region.Left - 4 && box[0] < region.Right
                && box[1] + box[3] / 2 >= region.Top && box[1] + box[3] / 2 < region.Bottom).ToArray();
            if (fragments.Any(row => resolvedText.Contains(Text(row)))) continue;
            var request = new MaaDynamicOcrRequest($"{EntryPrefix}band.{attempts.Count}",
                region.Left, region.Top, region.Width, region.Height, 4, 0);
            var result = await recognize(request, cancellationToken);
            attempts.Add(result);
            var recognized = Rows(Parse(result.RecognitionDetailJson));
            var corroborated = false;
            var acceptedScore = recognized.Count == 1 ? Score(recognized[0]) : 0;
            if (result.Succeeded && result.Hit && result.Entry == request.Entry && recognized.Count == 1
                && Score(recognized[0]) is >= 0.75 and < 0.90 && attempts.Count < MaximumRetriesPerFrame
                && names.ContainsKey(TitleKey(Text(recognized[0]))))
            {
                var firstKey = TitleKey(Text(recognized[0]));
                var firstScore = Score(recognized[0]);
                request = request with { Entry = $"{EntryPrefix}band.{attempts.Count}.fallback", Scale = 3 };
                result = await recognize(request, cancellationToken);
                attempts.Add(result);
                recognized = Rows(Parse(result.RecognitionDetailJson));
                if (recognized.Count != 1 || TitleKey(Text(recognized[0])) != firstKey) continue;
                // A borderline read needs agreement at a second scale. Both reads
                // must spell the complete catalog name, and confidence is not raised.
                corroborated = firstScore >= 0.85 && Score(recognized[0]) >= 0.85;
                acceptedScore = Math.Min(firstScore, Score(recognized[0]));
            }
            if (result.Succeeded && result.Hit && result.Entry == request.Entry && recognized.Count == 1
                && (Score(recognized[0]) >= 0.90 || corroborated)
                && names.TryGetValue(TitleKey(Text(recognized[0])), out var name))
            {
                foreach (var fragment in fragments.Where(row => !IsUsageLabel(Text(row)))) rows.Remove(fragment);
                rows.Add(new JsonObject
                {
                    ["text"] = name, ["score"] = acceptedScore,
                    ["box"] = new JsonArray(region.Left, region.Top, region.Width, region.Height),
                    ["rhodes_name_retry"] = new JsonObject
                    {
                        ["original_text"] = string.Join(" ", fragments.Select(Text)),
                        ["original_score"] = fragments.Select(Score).DefaultIfEmpty(0).Min(),
                        ["entry"] = request.Entry,
                    },
                });
                changed = true;
            }
            if (attempts.Count >= MaximumRetriesPerFrame) break;
        }
        return new MaaRelicTitleRefinement(changed ? frame with { RecognitionDetailJson = document.ToJsonString(), Hit = true } : frame, attempts);
    }

    private static string TitleKey(string text) => RhodesMaaLocalCandidateConverter.NormalizeRelicName(text);

    private static bool IsUsageLabel(string text) => text.StartsWith("使用", StringComparison.Ordinal)
        && text.Length <= 4 && !text.Contains("使用後", StringComparison.Ordinal);

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
