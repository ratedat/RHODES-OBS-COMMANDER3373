using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record MaaThoughtNameRefinement(
    MaaTaskRunResult Frame,
    IReadOnlyList<MaaTaskRunResult> Attempts);

public static class RhodesMaaThoughtNameOcrExpander
{
    public const int MaximumRetriesPerFrame = 8;
    private const string ListEntry = "RhodesOcrRegion_is5_thought_list_text";
    private const int Padding = 5;
    private const double MinimumRetryScore = 0.90;

    public static IReadOnlyList<MaaDynamicOcrRequest> BuildRequests(MaaTaskRunResult frame) =>
        CreatePlan(frame)?.Targets.Select(target => target.Request).ToArray() ?? [];

    public static async Task<MaaThoughtNameRefinement> RefineAsync(
        MaaTaskRunResult frame,
        Func<MaaDynamicOcrRequest, CancellationToken, Task<MaaTaskRunResult>> recognize,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var plan = CreatePlan(frame);
        if (plan is null || plan.Targets.Count == 0 && !plan.HasMasterCorrections)
            return new MaaThoughtNameRefinement(frame, []);

        var attempts = new List<MaaTaskRunResult>();
        var changed = plan.HasMasterCorrections;
        foreach (var target in plan.Targets)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await recognize(target.Request, cancellationToken);
            attempts.Add(result);
            if (!result.Succeeded || !result.Hit || result.Entry != target.Request.Entry)
                continue;
            var retryDocument = Parse(result.RecognitionDetailJson);
            var retryRows = PrimaryRows(retryDocument);
            if (retryRows.Count != 1)
                continue;
            var normalized = Normalize(Text(retryRows[0]));
            var score = Score(retryRows[0]);
            if (score < MinimumRetryScore || !plan.Names.TryGetValue(normalized, out var formalName)
                || normalized != target.ExpectedName)
                continue;

            // Keep this row in its original frame. Crop coordinates belong to the scaled
            // retry image and must never be used for card tracking or the load-value ROI.
            target.Row["rhodes_name_retry"] = new JsonObject
            {
                ["original_text"] = Text(target.Row),
                ["original_score"] = Score(target.Row),
                ["entry"] = result.Entry,
            };
            target.Row["text"] = formalName;
            target.Row["score"] = score;
            changed = true;
        }
        return new MaaThoughtNameRefinement(
            changed ? frame with { RecognitionDetailJson = plan.Document.ToJsonString() } : frame,
            attempts);
    }

    private static Plan? CreatePlan(MaaTaskRunResult frame)
    {
        if (!frame.Succeeded || !frame.Hit || frame.Entry != ListEntry)
            return null;
        var document = Parse(frame.RecognitionDetailJson);
        if (document is null)
            return null;
        var names = RhodesRunCatalog.LoadSpecialEffectOptions("is5_sarkaz", "thought")
            .GroupBy(item => Normalize(item.Name), StringComparer.Ordinal)
            .Where(group => group.Key.Length >= 2 && group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().Name, StringComparer.Ordinal);
        var rows = PrimaryRows(document);
        var nameCorrections = RhodesOcrNameCorrections.Load();
        var hasMasterCorrections = false;
        foreach (var row in rows)
        {
            if (names.ContainsKey(Normalize(Text(row)))) continue;
            var correction = nameCorrections.Resolve("thought", "is5_sarkaz", Text(row));
            if (correction is null || !names.ContainsKey(Normalize(correction.CanonicalName))) continue;
            row["rhodes_name_correction"] = new JsonObject
            {
                ["original_text"] = Text(row),
                ["original_score"] = Score(row),
                ["rule_id"] = correction.RuleId,
            };
            row["text"] = correction.CanonicalName;
            hasMasterCorrections = true;
        }
        var knownTitles = rows
            .Where(row => names.ContainsKey(Normalize(Text(row))))
            .Select(Box).Where(box => box is not null).Select(box => box!.Value)
            .Where(IsInsideList).ToArray();
        // Effect prose can itself mention a formal thought name. A lone exact word
        // is not an anchor until another title establishes a column and row spacing.
        var anchors = knownTitles.Where(title => knownTitles.Any(other =>
            Math.Abs(title.X - other.X) <= 12 && Math.Abs(title.Y - other.Y) is >= 64 and <= 200)).ToArray();
        var targets = new List<Target>();
        var positions = new HashSet<(int X, int Y)>();
        foreach (var row in rows)
        {
            var normalized = Normalize(Text(row));
            if (normalized.Length < 2 || names.ContainsKey(normalized)
                || Box(row) is not { } box || !IsInsideList(box)
                || !IsTitleAligned(box, anchors) || !positions.Add((box.X, box.Y)))
                continue;
            var nearNames = names.Keys.Where(name => IsOneEditApart(normalized, name)).Take(2).ToArray();
            if (nearNames.Length != 1)
                continue;
            targets.Add(new Target(
                new MaaDynamicOcrRequest($"thought.card.name.retry.{targets.Count}",
                    box.X - Padding, box.Y - Padding, box.Width + Padding * 2, box.Height + Padding * 2,
                    4, Score(row)),
                row, nearNames[0]));
            if (targets.Count >= MaximumRetriesPerFrame)
                break;
        }
        return new Plan(document, names, targets, hasMasterCorrections);
    }

    private static bool IsInsideList(TextBox box) =>
        box.X >= 355 + Padding && box.Y >= 84 + Padding
        && box.Width is > 0 and <= 280 && box.Height is >= 10 and <= 40
        && (long)box.X + box.Width + Padding <= 1190
        && (long)box.Y + box.Height + Padding <= 624;

    private static bool IsTitleAligned(TextBox box, IReadOnlyList<TextBox> anchors)
    {
        // A known title in the other column identifies the same card row. Otherwise
        // infer the vertical spacing from consecutive known titles in this column.
        if (anchors.Any(anchor => Math.Abs(anchor.X - box.X) is >= 300 and <= 500
            && Math.Abs(anchor.Y - box.Y) <= 8))
            return true;
        var column = anchors.Where(anchor => Math.Abs(anchor.X - box.X) <= 12)
            .OrderBy(anchor => anchor.Y).ToArray();
        var spacing = column.Zip(column.Skip(1), (left, right) => right.Y - left.Y)
            .Where(distance => distance is >= 64 and <= 200).DefaultIfEmpty(0).Min();
        return spacing > 0 && column.Any(anchor =>
        {
            var distance = Math.Abs(anchor.Y - box.Y);
            var steps = (int)Math.Round(distance / (double)spacing);
            return steps is >= 1 and <= 6 && Math.Abs(distance - steps * spacing) <= 8;
        });
    }

    private static bool IsOneEditApart(string left, string right)
    {
        if (Math.Abs(left.Length - right.Length) > 1)
            return false;
        var i = 0;
        var j = 0;
        var edits = 0;
        while (i < left.Length && j < right.Length)
        {
            if (left[i] == right[j]) { i++; j++; continue; }
            if (++edits > 1) return false;
            if (left.Length >= right.Length) i++;
            if (right.Length >= left.Length) j++;
        }
        return edits + (left.Length - i) + (right.Length - j) == 1;
    }

    private static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormKC)
        .Where(ch => !char.IsWhiteSpace(ch) && ch is not ('「' or '」' or '『' or '』' or '【' or '】' or '[' or ']' or '(' or ')' or '・')));

    private static JsonObject? Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return JsonNode.Parse(value) as JsonObject; }
        catch (JsonException) { return null; }
    }

    private static IReadOnlyList<JsonObject> PrimaryRows(JsonObject? document)
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

    private static string Text(JsonObject row) =>
        row["text"] is JsonValue value && value.TryGetValue<string>(out var text) ? text : "";

    private static double Score(JsonObject row) =>
        row["score"] is JsonValue value && value.TryGetValue<double>(out var score) && double.IsFinite(score)
            ? Math.Clamp(score, 0, 1) : 0;

    private static TextBox? Box(JsonObject row)
    {
        if (row["box"] is not JsonArray { Count: 4 } box) return null;
        var values = new int[4];
        for (var index = 0; index < 4; index++)
            if (box[index] is not JsonValue value || !value.TryGetValue<int>(out values[index])) return null;
        return new TextBox(values[0], values[1], values[2], values[3]);
    }

    private readonly record struct TextBox(int X, int Y, int Width, int Height);
    private sealed record Target(MaaDynamicOcrRequest Request, JsonObject Row, string ExpectedName);
    private sealed record Plan(JsonObject Document, IReadOnlyDictionary<string, string> Names, IReadOnlyList<Target> Targets, bool HasMasterCorrections);
}
