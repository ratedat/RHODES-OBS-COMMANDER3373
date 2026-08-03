using System.Text.Json;
using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

public sealed record RhodesOperatorPromotionCardDetection(
    bool IsEliteTwo,
    double MarkerRatio,
    int MarkerPixels,
    MaaRoi Roi);

public static class RhodesOperatorPromotionCardDetector
{
    public const string EntryPrefix = "RhodesOperatorPromotionCard_";

    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const int MarkerOffsetX = -183;
    private const int MarkerOffsetY = -64;
    private const int MarkerWidth = 53;
    private const int MarkerHeight = 53;
    // Real 1280x720 operator cards expose only about 5.3-5.7% warm glow in this ROI.
    private const double MarkerRatioThreshold = 0.045;
    private const int MinimumMarkerPixels = 80;

    public static RhodesOperatorPromotionCardDetection Detect(
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
                if (IsEliteTwoGlow(bitmap.GetPixel(x, y)))
                    markerPixels++;
            }
        }

        var markerRatio = sampledPixels == 0 ? 0 : (double)markerPixels / sampledPixels;
        return new RhodesOperatorPromotionCardDetection(
            markerRatio >= MarkerRatioThreshold && markerPixels >= MinimumMarkerPixels,
            markerRatio,
            markerPixels,
            roi);
    }

    public static MaaTaskRunResult CreateTaskResult(
        MaaDynamicOcrRequest nameRequest,
        MaaCandidatePreview operatorCandidate,
        RhodesOperatorPromotionCardDetection detection,
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
            promotionLevel = 2,
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
            $"eliteTwoGlowRatio={detection.MarkerRatio:0.###}",
            detail,
            "ColorAnalysis",
            detection.IsEliteTwo);
    }

    public static bool TryRead(
        MaaTaskRunResult taskResult,
        out string operatorId,
        out string label,
        out int promotionLevel,
        out double score,
        out int operatorInstance)
    {
        operatorId = "";
        label = "";
        promotionLevel = 0;
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
            promotionLevel = root.TryGetProperty("promotionLevel", out var promotionProperty)
                && promotionProperty.TryGetInt32(out var parsedPromotion)
                    ? parsedPromotion
                    : 2;
            score = root.TryGetProperty("score", out var scoreProperty)
                && scoreProperty.TryGetDouble(out var parsedScore)
                    ? parsedScore
                    : 0.84;
            operatorInstance = root.TryGetProperty("operatorInstance", out var instanceProperty)
                && instanceProperty.TryGetInt32(out var parsedInstance)
                    ? Math.Max(1, parsedInstance)
                    : 1;
            return !string.IsNullOrWhiteSpace(operatorId) && promotionLevel >= 2;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static RhodesOperatorPromotionCardDetection EmptyDetection(MaaRoi roi) =>
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

    private static bool IsEliteTwoGlow(SKColor color) =>
        color.Red >= 180
        && color.Green is >= 65 and <= 210
        && color.Blue <= 110
        && color.Red >= color.Green + 30
        && color.Green >= color.Blue + 10;

    private static string StringProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
}
