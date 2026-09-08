using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

internal enum RhodesRelicStackObservationStatus
{
    Confirmed,
    ExplicitZero,
    Unreadable,
    LowConfidence,
    Ambiguous,
    OutOfRange,
}

internal sealed record RhodesRelicStackObservation(
    string RelicId,
    string CaptureId,
    string CardId,
    int? Count,
    RhodesRelicStackObservationStatus Status);

internal static partial class RhodesRelicStackObservationReader
{
    private const double MinimumAcceptedConfidence = 0.50;

    public static IReadOnlyList<RhodesRelicStackObservation> Read(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var observations = new List<RhodesRelicStackObservation>();
        var legacyIndex = 0;
        foreach (var result in taskResults)
        {
            if (!RhodesRelicStackOcrPlanner.TryParseEntry(result.Entry, out var relicId, out var captureId, out var cardId)
                || RhodesRelicStackRuleCatalog.Find(relicId) is null
                || RhodesRelicStackRuleCatalog.IsExplicitlyNonStack(relicId))
                continue;

            if (captureId.Length == 0)
            {
                captureId = $"legacy-{legacyIndex}";
                cardId = captureId;
                legacyIndex++;
            }

            observations.Add(ReadOne(result, relicId, captureId, cardId));
        }
        return observations;
    }

    public static IReadOnlyDictionary<string, int> ResolveCounts(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var captureVotes = Read(taskResults)
            .GroupBy(item => (item.RelicId, item.CaptureId))
            .Select(group =>
            {
                var cardValues = group
                    .GroupBy(item => item.CardId, StringComparer.Ordinal)
                    .Select(card =>
                    {
                        var values = card.Where(item => item.Status == RhodesRelicStackObservationStatus.Confirmed)
                            .Select(item => item.Count!.Value).Distinct().ToArray();
                        return values.Length == 1 ? values[0] : (int?)null;
                    })
                    .ToArray();
                var distinct = cardValues.Where(value => value is not null).Select(value => value!.Value).Distinct().ToArray();
                return cardValues.Length > 0 && cardValues.All(value => value is not null) && distinct.Length == 1
                    ? (group.Key.RelicId, Count: (int?)distinct[0])
                    : (group.Key.RelicId, Count: (int?)null);
            })
            .Where(item => item.Count is not null)
            .GroupBy(item => item.RelicId, StringComparer.Ordinal);

        var resolved = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var relic in captureVotes)
        {
            var ranked = relic.GroupBy(item => item.Count!.Value)
                .Select(group => (Count: group.Key, Votes: group.Count()))
                .OrderByDescending(item => item.Votes)
                .ThenBy(item => item.Count)
                .ToArray();
            if (ranked.Length == 0 || ranked.Length > 1 && ranked[0].Votes == ranked[1].Votes)
                continue;
            resolved[relic.Key] = ranked[0].Count;
        }
        return resolved;
    }

    public static bool IsConfirmed(MaaTaskRunResult taskResult) =>
        Read([taskResult]).SingleOrDefault()?.Status == RhodesRelicStackObservationStatus.Confirmed;

    private static RhodesRelicStackObservation ReadOne(
        MaaTaskRunResult result,
        string relicId,
        string captureId,
        string cardId)
    {
        if (!result.Succeeded || !result.Hit)
            return new(relicId, captureId, cardId, null, RhodesRelicStackObservationStatus.Unreadable);

        var texts = PrimaryTexts(result.RecognitionDetailJson);
        if (texts.Count == 0)
            return new(relicId, captureId, cardId, null, RhodesRelicStackObservationStatus.Unreadable);
        if (texts.Any(item => item.Confidence is double confidence && confidence < MinimumAcceptedConfidence))
            return new(relicId, captureId, cardId, null, RhodesRelicStackObservationStatus.LowConfidence);

        var values = new List<int>();
        var invalid = false;
        foreach (var item in texts)
        {
            var match = StackNumberRegex().Match(item.Text.Normalize(NormalizationForm.FormKC).Trim());
            if (match.Success
                && int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                values.Add(value);
            else
                invalid = true;
        }

        var distinct = values.Distinct().ToArray();
        if (values.Count == 0)
            return new(relicId, captureId, cardId, null, RhodesRelicStackObservationStatus.Unreadable);
        if (invalid || distinct.Length != 1)
            return new(relicId, captureId, cardId, null, RhodesRelicStackObservationStatus.Ambiguous);

        var count = distinct[0];
        if (count == 0)
            return new(relicId, captureId, cardId, 0, RhodesRelicStackObservationStatus.ExplicitZero);
        return RhodesRelicStackRuleCatalog.IsWithinKnownLimit(relicId, count)
            ? new(relicId, captureId, cardId, count, RhodesRelicStackObservationStatus.Confirmed)
            : new(relicId, captureId, cardId, count, RhodesRelicStackObservationStatus.OutOfRange);
    }

    private static IReadOnlyList<OcrText> PrimaryTexts(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            var start = json.IndexOf('{');
            if (start < 0) return [];
            using var document = JsonDocument.Parse(json[start..]);
            var root = document.RootElement;
            if (root.TryGetProperty("result", out var nested) && nested.ValueKind == JsonValueKind.Object)
                root = nested;
            foreach (var key in new[] { "filtered", "filtered_results", "filteredResults" })
                if (root.TryGetProperty(key, out var filtered) && Rows(filtered) is { Count: > 0 } filteredRows)
                    return Texts(filteredRows);
            foreach (var key in new[] { "best", "best_result", "bestResult" })
                if (root.TryGetProperty(key, out var best) && best.ValueKind == JsonValueKind.Object)
                    return Texts([best]);
            foreach (var key in new[] { "all", "all_results", "allResults" })
                if (root.TryGetProperty(key, out var all) && Rows(all) is { Count: > 0 } allRows)
                    return Texts(allRows);
        }
        catch (JsonException)
        {
        }
        return [];
    }

    private static IReadOnlyList<JsonElement> Rows(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => value.EnumerateArray().ToArray(),
        JsonValueKind.Object => [value],
        _ => [],
    };

    private static IReadOnlyList<OcrText> Texts(IEnumerable<JsonElement> rows) => rows
        .Where(row => row.ValueKind == JsonValueKind.Object
            && row.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
        .Select(row => new OcrText(
            row.GetProperty("text").GetString() ?? "",
            Confidence(row)))
        .Where(item => !string.IsNullOrWhiteSpace(item.Text))
        .ToArray();

    private static double? Confidence(JsonElement row)
    {
        foreach (var key in new[] { "score", "confidence", "prob" })
            if (row.TryGetProperty(key, out var value)
                && value.ValueKind == JsonValueKind.Number
                && value.TryGetDouble(out var confidence))
                return confidence;
        return null;
    }

    private sealed record OcrText(string Text, double? Confidence);

    [GeneratedRegex(@"\A(?:[×xX^★-]\s*)?([0-9]+)\z", RegexOptions.CultureInvariant)]
    private static partial Regex StackNumberRegex();
}
