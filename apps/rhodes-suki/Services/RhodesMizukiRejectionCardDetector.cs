using System.Text.Json;
using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

public sealed record RhodesMizukiRejectionCardDetection(
    bool IsAffected,
    double PurpleRatio,
    MaaRoi Roi);

public static class RhodesMizukiRejectionCardDetector
{
    public const string EntryPrefix = "RhodesMizukiRejectionCard_";

    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const double AffectedThreshold = 0.025;

    public static RhodesMizukiRejectionCardDetection Detect(
        byte[] encodedImage,
        MaaDynamicOcrRequest nameRequest)
    {
        var roi = ClampBaseRoi(new MaaRoi(
            nameRequest.X,
            nameRequest.Y,
            nameRequest.Width,
            nameRequest.Height));
        if (encodedImage.Length == 0 || roi.Width <= 0 || roi.Height <= 0)
            return new RhodesMizukiRejectionCardDetection(false, 0, roi);

        using var bitmap = SKBitmap.Decode(encodedImage);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            return new RhodesMizukiRejectionCardDetection(false, 0, roi);

        var left = Scale(roi.X, bitmap.Width, BaseWidth);
        var top = Scale(roi.Y, bitmap.Height, BaseHeight);
        var right = Scale(roi.X + roi.Width, bitmap.Width, BaseWidth);
        var bottom = Scale(roi.Y + roi.Height, bitmap.Height, BaseHeight);
        left = Math.Clamp(left, 0, bitmap.Width - 1);
        top = Math.Clamp(top, 0, bitmap.Height - 1);
        right = Math.Clamp(right, left + 1, bitmap.Width);
        bottom = Math.Clamp(bottom, top + 1, bitmap.Height);

        var purplePixels = 0;
        var sampledPixels = 0;
        for (var y = top; y < bottom; y++)
        {
            for (var x = left; x < right; x++)
            {
                sampledPixels++;
                if (IsRejectionPurple(bitmap.GetPixel(x, y)))
                    purplePixels++;
            }
        }

        var ratio = sampledPixels == 0 ? 0 : (double)purplePixels / sampledPixels;
        return new RhodesMizukiRejectionCardDetection(ratio >= AffectedThreshold, ratio, roi);
    }

    public static MaaTaskRunResult CreateTaskResult(
        MaaDynamicOcrRequest nameRequest,
        MaaCandidatePreview operatorCandidate,
        RhodesMizukiRejectionCardDetection detection)
    {
        var operatorId = operatorCandidate.OperatorId.Trim();
        var score = Math.Clamp(0.82 + (detection.PurpleRatio * 0.25), 0.82, 0.99);
        var detail = JsonSerializer.Serialize(new
        {
            operatorId,
            label = operatorCandidate.Label,
            score,
            purpleTextRatio = detection.PurpleRatio,
            roi = detection.Roi.ToArray(),
            sourceEntry = nameRequest.Entry,
        });
        return new MaaTaskRunResult(
            $"{EntryPrefix}{operatorId}",
            "Succeeded",
            true,
            $"purpleTextRatio={detection.PurpleRatio:0.###}",
            detail,
            "ColorAnalysis",
            detection.IsAffected);
    }

    public static bool TryRead(
        MaaTaskRunResult taskResult,
        out string operatorId,
        out string label,
        out double score)
    {
        operatorId = "";
        label = "";
        score = 0;
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
                    : 0.82;
            return !string.IsNullOrWhiteSpace(operatorId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

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

    private static bool IsRejectionPurple(SKColor color)
    {
        var red = color.Red / 255d;
        var green = color.Green / 255d;
        var blue = color.Blue / 255d;
        var maximum = Math.Max(red, Math.Max(green, blue));
        var minimum = Math.Min(red, Math.Min(green, blue));
        var delta = maximum - minimum;
        if (maximum < 0.12 || delta <= 0)
            return false;

        var saturation = delta / maximum;
        if (saturation < 0.20)
            return false;

        var hue = maximum == red
            ? 60 * (((green - blue) / delta) % 6)
            : maximum == green
                ? 60 * (((blue - red) / delta) + 2)
                : 60 * (((red - green) / delta) + 4);
        if (hue < 0)
            hue += 360;
        return hue is >= 260 and <= 335;
    }

    private static string StringProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";
}
