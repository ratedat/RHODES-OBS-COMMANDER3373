using System.Text.Json;
using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

public static class RhodesRelicStackOcrPlanner
{
    public const string EntryPrefix = "relic.stack.";
    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const int MarkerProbeOffsetX = -62;
    private const int MarkerProbeOffsetY = 64;
    private const int MarkerProbeWidth = 55;
    private const int MarkerProbeHeight = 45;
    private const int OcrOffsetX = -96;
    private const int OcrOffsetY = 46;
    private const int OcrWidth = 90;
    private const int OcrHeight = 70;
    private const int OcrScale = 6;
    private const double OcrThreshold = 0.1;
    private const double MinimumNeutralBrightRatio = 0.012;

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
        foreach (var taskResult in taskResults)
        {
            if (!taskResult.Succeeded || !IsRelicNameEntry(taskResult.Entry))
                continue;

            foreach (var row in ReadOcrRows(taskResult.RecognitionDetailJson))
            {
                if (row.X < 0 || row.Y < 0 || row.Width <= 0 || row.Height <= 0)
                    continue;

                var relic = RhodesMaaLocalCandidateConverter.ResolveRelicName(row.Text, campaignId);
                if (relic is null || !emitted.Add(relic.Id))
                    continue;

                var probe = new SKRectI(
                    row.X + MarkerProbeOffsetX,
                    row.Y + MarkerProbeOffsetY,
                    row.X + MarkerProbeOffsetX + MarkerProbeWidth,
                    row.Y + MarkerProbeOffsetY + MarkerProbeHeight);
                if (!HasStackMarker(bitmap, probe))
                    continue;

                var x = row.X + OcrOffsetX;
                var y = row.Y + OcrOffsetY;
                if (x < 0 || y < 0 || x + OcrWidth > BaseWidth || y + OcrHeight > BaseHeight)
                    continue;

                requests.Add(new MaaDynamicOcrRequest(
                    $"{EntryPrefix}{relic.Id}",
                    x,
                    y,
                    OcrWidth,
                    OcrHeight,
                    OcrScale,
                    row.Confidence ?? 0,
                    OnlyRecognition: false,
                    Threshold: OcrThreshold));
            }
        }
        return requests;
    }

    private static bool HasStackMarker(SKBitmap bitmap, SKRectI requested)
    {
        var xScale = bitmap.Width / (double)BaseWidth;
        var yScale = bitmap.Height / (double)BaseHeight;
        var left = Math.Clamp((int)Math.Floor(requested.Left * xScale), 0, bitmap.Width);
        var top = Math.Clamp((int)Math.Floor(requested.Top * yScale), 0, bitmap.Height);
        var right = Math.Clamp((int)Math.Ceiling(requested.Right * xScale), 0, bitmap.Width);
        var bottom = Math.Clamp((int)Math.Ceiling(requested.Bottom * yScale), 0, bitmap.Height);
        if (right <= left || bottom <= top)
            return false;

        var bright = 0;
        var total = (right - left) * (bottom - top);
        for (var y = top; y < bottom; y++)
        for (var x = left; x < right; x++)
        {
            var color = bitmap.GetPixel(x, y);
            if (color.Red >= 205 && color.Green >= 205 && color.Blue >= 205)
                bright++;
        }
        return bright >= Math.Max(12, (int)Math.Ceiling(total * MinimumNeutralBrightRatio));
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
            if (item.TryGetProperty(propertyName, out var score) && score.TryGetDouble(out var number))
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
