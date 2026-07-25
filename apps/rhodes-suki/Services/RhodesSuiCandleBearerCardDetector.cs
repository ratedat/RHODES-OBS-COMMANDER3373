using System.Text.Json;
using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

public sealed record RhodesSuiCandleBearerCardDetection(
    bool IsCandleBearer,
    double MarkerRatio,
    int MarkerPixels,
    MaaRoi Roi);

public static class RhodesSuiCandleBearerCardDetector
{
    public const string EntryPrefix = "RhodesSuiCandleBearerCard_";

    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const int MarkerOffsetX = -207;
    private const int MarkerOffsetY = -10;
    private const int MarkerWidth = 24;
    private const int MarkerHeight = 30;
    private const double MarkerRatioThreshold = 0.12;
    private const int MinimumMarkerPixels = 48;

    public static RhodesSuiCandleBearerCardDetection Detect(
        byte[] encodedImage,
        MaaDynamicOcrRequest nameRequest)
    {
        var roi = ClampBaseRoi(new MaaRoi(
            nameRequest.X + MarkerOffsetX,
            nameRequest.Y + MarkerOffsetY,
            MarkerWidth,
            MarkerHeight));
        if (encodedImage.Length == 0 || roi.Width <= 0 || roi.Height <= 0)
            return EmptyDetection(roi);

        using var bitmap = SKBitmap.Decode(encodedImage);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            return EmptyDetection(roi);

        var left = Math.Clamp(Scale(roi.X, bitmap.Width, BaseWidth), 0, bitmap.Width - 1);
        var top = Math.Clamp(Scale(roi.Y, bitmap.Height, BaseHeight), 0, bitmap.Height - 1);
        var right = Math.Clamp(Scale(roi.X + roi.Width, bitmap.Width, BaseWidth), left + 1, bitmap.Width);
        var bottom = Math.Clamp(Scale(roi.Y + roi.Height, bitmap.Height, BaseHeight), top + 1, bitmap.Height);
        var markerPixels = 0;
        var sampledPixels = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                sampledPixels++;
                if (IsCandleBearerPink(bitmap.GetPixel(x, y)))
                    markerPixels++;
            }
        }

        var markerRatio = sampledPixels == 0 ? 0 : (double)markerPixels / sampledPixels;
        return new RhodesSuiCandleBearerCardDetection(
            markerRatio >= MarkerRatioThreshold && markerPixels >= MinimumMarkerPixels,
            markerRatio,
            markerPixels,
            roi);
    }

    public static MaaTaskRunResult CreateTaskResult(
        MaaDynamicOcrRequest nameRequest,
        MaaCandidatePreview operatorCandidate,
        RhodesSuiCandleBearerCardDetection detection,
        int operatorInstance = 1)
    {
        var operatorId = operatorCandidate.OperatorId.Trim();
        operatorInstance = Math.Max(1, operatorInstance);
        var score = Math.Clamp(0.84 + (detection.MarkerRatio * 0.15), 0.84, 0.99);
        var detail = JsonSerializer.Serialize(new
        {
            operatorId,
            operatorInstance,
            label = operatorCandidate.Label,
            score,
            markerRatio = detection.MarkerRatio,
            markerPixels = detection.MarkerPixels,
            roi = detection.Roi.ToArray(),
            sourceEntry = nameRequest.Entry,
        });
        return new MaaTaskRunResult(
            $"{EntryPrefix}{operatorId}_{operatorInstance}",
            "Succeeded",
            true,
            $"pinkMarkerRatio={detection.MarkerRatio:0.###}",
            detail,
            "ColorAnalysis",
            detection.IsCandleBearer);
    }

    public static bool TryRead(
        MaaTaskRunResult taskResult,
        out string operatorId,
        out string label,
        out double score,
        out int operatorInstance)
    {
        operatorId = "";
        label = "";
        score = 0;
        operatorInstance = 1;
        if (!taskResult.Succeeded
            || !taskResult.Hit
            || !taskResult.Entry.StartsWith(EntryPrefix, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(taskResult.RecognitionDetailJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(taskResult.RecognitionDetailJson);
            var root = document.RootElement;
            operatorId = StringProperty(root, "operatorId");
            label = StringProperty(root, "label");
            score = root.TryGetProperty("score", out var scoreProperty)
                && scoreProperty.TryGetDouble(out var parsedScore)
                    ? parsedScore
                    : 0.84;
            operatorInstance = root.TryGetProperty("operatorInstance", out var instanceProperty)
                && instanceProperty.TryGetInt32(out var parsedInstance)
                    ? Math.Max(1, parsedInstance)
                    : 1;
            return !string.IsNullOrWhiteSpace(operatorId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static RhodesSuiCandleBearerCardDetection EmptyDetection(MaaRoi roi) =>
        new(false, 0, 0, roi);

    private static MaaRoi ClampBaseRoi(MaaRoi roi)
    {
        var x = Math.Clamp(roi.X, 0, BaseWidth);
        var y = Math.Clamp(roi.Y, 0, BaseHeight);
        var right = Math.Clamp(roi.X + roi.Width, x, BaseWidth);
        var bottom = Math.Clamp(roi.Y + roi.Height, y, BaseHeight);
        return new MaaRoi(x, y, right - x, bottom - y);
    }

    private static int Scale(int value, int actual, int basis) =>
        (int)Math.Round(value * (double)actual / basis, MidpointRounding.AwayFromZero);

    private static bool IsCandleBearerPink(SKColor color)
    {
        var red = color.Red / 255d;
        var green = color.Green / 255d;
        var blue = color.Blue / 255d;
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        var delta = maximum - minimum;
        if (maximum < 0.35 || delta <= 0)
            return false;

        var saturation = delta / maximum;
        if (saturation < 0.32)
            return false;

        var hue = maximum == red
            ? 60 * (((green - blue) / delta) % 6)
            : maximum == green
                ? 60 * (((blue - red) / delta) + 2)
                : 60 * (((red - green) / delta) + 4);
        if (hue < 0)
            hue += 360;
        return hue is >= 320 and <= 355;
    }

    private static string StringProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
}
