using System.Text.Json;
using System.Security.Cryptography;
using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

public static class RhodesRelicStackOcrPlanner
{
    public const string EntryPrefix = "relic.stack.";
    private const string CaptureSeparator = ".capture.";
    private const string CardSeparator = ".card.";
    private const string VariantSeparator = ".variant.";
    private const string PrimaryVariant = "gray";
    private const string FallbackVariant = "binary";
    private const string RescueVariant = "gray0";
    private const string PaddedBinaryVariant = "binary-pad2";
    private const string PaddedGrayVariant = "gray0-pad2";
    private const int OcrScale = 8;
    private const double OcrThreshold = 0.1;

    public static IReadOnlyList<MaaDynamicOcrRequest> BuildRequests(
        IEnumerable<MaaTaskRunResult> taskResults,
        byte[]? encodedImage,
        string campaignId)
    {
        if (encodedImage is null
            || encodedImage.Length == 0
            || string.IsNullOrWhiteSpace(campaignId))
        {
            return [];
        }

        using var bitmap = SKBitmap.Decode(encodedImage);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            return [];

        var requests = new List<MaaDynamicOcrRequest>();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var captureId = Convert.ToHexString(SHA256.HashData(encodedImage).AsSpan(0, 6)).ToLowerInvariant();
        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsRelicNameEntry(taskResult.Entry))
                continue;

            foreach (var row in ReadOcrRows(taskResult.RecognitionDetailJson))
            {
                if (row.X < 0 || row.Y < 0 || row.Width <= 0 || row.Height <= 0)
                    continue;

                var relic = RhodesMaaLocalCandidateConverter.ResolveRelicName(row.Text, campaignId);
                if (relic is null
                    || RhodesRelicStackRuleCatalog.Find(relic.Id) is null
                    || RhodesRelicStackRuleCatalog.IsExplicitlyNonStack(relic.Id))
                    continue;

                if (!RhodesRelicStackImageSeparator.TryLocateDigits(bitmap, row.X, row.Y, out var digits))
                    continue;

                var entry = CreateEntry(relic.Id, captureId, row.X, row.Y);
                if (!emitted.Add(entry))
                    continue;

                requests.Add(new MaaDynamicOcrRequest(
                    entry,
                    digits.Left,
                    digits.Top,
                    digits.Width,
                    digits.Height,
                    OcrScale,
                    row.Confidence ?? 0,
                    OnlyRecognition: true,
                    Threshold: OcrThreshold));
            }
        }
        return requests;
    }

    internal static bool TryParseEntry(string? entry, out string relicId, out string captureId, out string cardId)
        => TryParseEntry(entry, out relicId, out captureId, out cardId, out _);

    internal static bool TryParseEntry(
        string? entry,
        out string relicId,
        out string captureId,
        out string cardId,
        out string variant)
    {
        relicId = "";
        captureId = "";
        cardId = "";
        variant = "";
        if (string.IsNullOrWhiteSpace(entry) || !entry.StartsWith(EntryPrefix, StringComparison.Ordinal))
            return false;

        var suffix = entry[EntryPrefix.Length..];
        var captureIndex = suffix.IndexOf(CaptureSeparator, StringComparison.Ordinal);
        if (captureIndex < 0)
        {
            relicId = suffix;
            return relicId.Length > 0;
        }

        var cardIndex = suffix.IndexOf(CardSeparator, captureIndex + CaptureSeparator.Length, StringComparison.Ordinal);
        if (captureIndex == 0 || cardIndex <= captureIndex + CaptureSeparator.Length)
            return false;
        relicId = suffix[..captureIndex];
        captureId = suffix[(captureIndex + CaptureSeparator.Length)..cardIndex];
        var cardSuffix = suffix[(cardIndex + CardSeparator.Length)..];
        var variantIndex = cardSuffix.IndexOf(VariantSeparator, StringComparison.Ordinal);
        if (variantIndex < 0)
            cardId = cardSuffix;
        else
        {
            cardId = cardSuffix[..variantIndex];
            variant = cardSuffix[(variantIndex + VariantSeparator.Length)..];
        }
        return cardId.Length > 0 && (variantIndex < 0 || variant.Length > 0);
    }

    private static string CreateEntry(string relicId, string captureId, int x, int y) =>
        $"{EntryPrefix}{relicId}{CaptureSeparator}{captureId}{CardSeparator}{x}_{y}{VariantSeparator}{PrimaryVariant}";

    internal static MaaDynamicOcrRequest? BuildFallbackRequest(MaaDynamicOcrRequest request)
    {
        if (!TryParseEntry(request.Entry, out _, out _, out _, out var variant)
            || variant is not (PrimaryVariant or FallbackVariant or RescueVariant))
            return null;
        var marker = $"{VariantSeparator}{variant}";
        var nextVariant = variant switch
        {
            PrimaryVariant => FallbackVariant,
            FallbackVariant => RescueVariant,
            _ => request.Width < request.Height ? PaddedBinaryVariant : PaddedGrayVariant,
        };
        return request with
        {
            Entry = request.Entry[..^marker.Length] + $"{VariantSeparator}{nextVariant}",
        };
    }

    private static bool IsRelicNameEntry(string entry)
    {
        return entry.Contains("relic", StringComparison.OrdinalIgnoreCase)
            && (entry.Contains("list_text", StringComparison.OrdinalIgnoreCase)
                || entry.Contains("detail_name", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<OcrRow> ReadOcrRows(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        try
        {
            using var document = JsonDocument.Parse(value[value.IndexOf('{')..]);
            var root = document.RootElement;
            if (root.TryGetProperty("result", out var nested) && nested.ValueKind == JsonValueKind.Object)
                root = nested;

            var items = ResultArray(root, "filtered", "filtered_results", "filteredResults");
            if (items.Count == 0)
                items = ResultArray(root, "all", "all_results", "allResults");
            if (items.Count == 0 && root.TryGetProperty("best", out var best) && best.ValueKind == JsonValueKind.Object)
                items.Add(best);

            return items.Select(ReadRow)
                .Where(row => !string.IsNullOrWhiteSpace(row.Text))
                .ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static List<JsonElement> ResultArray(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Array)
                return value.EnumerateArray().ToList();
            if (value.ValueKind == JsonValueKind.Object)
                return [value];
        }
        return [];
    }

    private static OcrRow ReadRow(JsonElement item)
    {
        var text = item.TryGetProperty("text", out var textValue) && textValue.ValueKind == JsonValueKind.String
            ? textValue.GetString() ?? ""
            : "";
        double? confidence = null;
        foreach (var propertyName in new[] { "score", "confidence", "prob" })
        {
            if (item.TryGetProperty(propertyName, out var score)
                && score.ValueKind == JsonValueKind.Number
                && score.TryGetDouble(out var number))
            {
                confidence = number;
                break;
            }
        }

        if (!item.TryGetProperty("box", out var box) || box.ValueKind != JsonValueKind.Array)
            return new OcrRow(text, confidence, -1, -1, 0, 0);
        var values = box.EnumerateArray().Take(4).ToArray();
        return values.Length == 4 && values.All(value => value.TryGetInt32(out _))
            ? new OcrRow(text, confidence, values[0].GetInt32(), values[1].GetInt32(), values[2].GetInt32(), values[3].GetInt32())
            : new OcrRow(text, confidence, -1, -1, 0, 0);
    }

    private sealed record OcrRow(
        string Text,
        double? Confidence,
        int X,
        int Y,
        int Width,
        int Height);
}
