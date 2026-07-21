using System.Text.Json;
using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

public sealed record RhodesSuiCoinImageDetection(
    string CoinId,
    string Label,
    double Score,
    int SlotIndex,
    MaaRoi Roi,
    string StatusId = "");

public static class RhodesSuiCoinImageRecognizer
{
    public const string Entry = "RhodesSuiCoinImage_activeCoins";
    public const string FieldId = "activeCoins";

    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const int FeatureSize = 32;
    private const double MinimumScore = 0.94;
    private const double MinimumMargin = 0.035;

    private static readonly int[] SlotSearchLeft = [592, 647, 702];
    private static readonly Lazy<IReadOnlyList<CoinTemplate>> DefaultTemplates = new(BuildDefaultTemplates);

    public static MaaTaskRunResult Recognize(
        byte[] encodedImage,
        IReadOnlyList<SukiSpecialEffectOption>? coinOptions = null)
    {
        var detections = Detect(encodedImage, coinOptions);
        var detail = JsonSerializer.Serialize(new
        {
            fieldId = FieldId,
            detections = detections.Select(detection => new
            {
                coinId = detection.CoinId,
                label = detection.Label,
                score = detection.Score,
                slotIndex = detection.SlotIndex,
                roi = detection.Roi.ToArray(),
                statusId = detection.StatusId,
            }),
        });
        return new MaaTaskRunResult(
            Entry,
            "Succeeded",
            true,
            $"activeCoins={detections.Count}",
            detail,
            "ImageClassification",
            detections.Count > 0);
    }

    public static IReadOnlyList<RhodesSuiCoinImageDetection> Detect(
        byte[] encodedImage,
        IReadOnlyList<SukiSpecialEffectOption>? coinOptions = null)
    {
        if (encodedImage.Length == 0)
            return [];

        using var decoded = SKBitmap.Decode(encodedImage);
        if (decoded is null || decoded.Width <= 0 || decoded.Height <= 0)
            return [];

        using var normalized = NormalizeFrame(decoded);
        var templates = coinOptions is null
            ? DefaultTemplates.Value
            : BuildTemplates(coinOptions);
        if (templates.Count == 0)
            return [];

        var detections = new List<RhodesSuiCoinImageDetection>();
        for (var slotIndex = 0; slotIndex < SlotSearchLeft.Length; slotIndex++)
        {
            var match = BestSlotMatch(normalized, templates, slotIndex);
            if (match is null
                || match.Score < MinimumScore
                || match.Score - match.RunnerUpScore < MinimumMargin)
            {
                continue;
            }

            detections.Add(new RhodesSuiCoinImageDetection(
                match.Template.Option.Id,
                match.Template.Option.Name,
                match.Score,
                slotIndex,
                match.Roi));
        }
        return detections;
    }

    public static bool TryRead(
        MaaTaskRunResult taskResult,
        out string fieldId,
        out IReadOnlyList<RhodesSuiCoinImageDetection> detections)
    {
        fieldId = "";
        detections = [];
        if (!taskResult.Succeeded
            || !taskResult.Entry.Equals(Entry, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(taskResult.RecognitionDetailJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(taskResult.RecognitionDetailJson);
            var root = document.RootElement;
            fieldId = StringProperty(root, "fieldId");
            if (!fieldId.Equals(FieldId, StringComparison.Ordinal)
                || !root.TryGetProperty("detections", out var detectionArray)
                || detectionArray.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            detections = detectionArray.EnumerateArray()
                .Select(ReadDetection)
                .Where(detection => detection is not null)
                .Cast<RhodesSuiCoinImageDetection>()
                .ToArray();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static SlotMatch? BestSlotMatch(
        SKBitmap frame,
        IReadOnlyList<CoinTemplate> templates,
        int slotIndex)
    {
        var bestByCoin = new Dictionary<string, SlotMatch>(StringComparer.Ordinal);
        for (var x = SlotSearchLeft[slotIndex]; x <= SlotSearchLeft[slotIndex] + 4; x++)
        {
            for (var y = 652; y <= 655; y++)
            {
                for (var size = 50; size <= 56; size++)
                {
                    var roi = new MaaRoi(x, y, size, size);
                    var pixels = CropFeature(frame, roi);
                    foreach (var template in templates)
                    {
                        var score = Similarity(pixels, template);
                        if (!bestByCoin.TryGetValue(template.Option.Id, out var current)
                            || score > current.Score)
                        {
                            bestByCoin[template.Option.Id] = new SlotMatch(template, score, 0, roi);
                        }
                    }
                }
            }
        }

        var ranked = bestByCoin.Values
            .OrderByDescending(match => match.Score)
            .Take(2)
            .ToArray();
        return ranked.Length == 0
            ? null
            : ranked[0] with { RunnerUpScore = ranked.ElementAtOrDefault(1)?.Score ?? 0 };
    }

    private static IReadOnlyList<CoinTemplate> BuildDefaultTemplates() =>
        BuildTemplates(RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin"));

    private static IReadOnlyList<CoinTemplate> BuildTemplates(IEnumerable<SukiSpecialEffectOption> options)
    {
        var templates = new List<CoinTemplate>();
        foreach (var option in options.Where(option => !string.IsNullOrWhiteSpace(option.ImagePath)))
        {
            using var bitmap = SKBitmap.Decode(option.ImagePath);
            if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
                continue;

            using var resized = Resize(bitmap, FeatureSize, FeatureSize);
            var pixels = new TemplatePixel[FeatureSize * FeatureSize];
            var index = 0;
            for (var y = 0; y < FeatureSize; y++)
            {
                for (var x = 0; x < FeatureSize; x++)
                {
                    var color = resized.GetPixel(x, y);
                    pixels[index++] = new TemplatePixel(color.Red, color.Green, color.Blue, color.Alpha);
                }
            }
            templates.Add(new CoinTemplate(option, pixels));
        }
        return templates;
    }

    private static SKBitmap NormalizeFrame(SKBitmap source)
    {
        if (source.Width == BaseWidth && source.Height == BaseHeight)
            return source.Copy();
        return Resize(source, BaseWidth, BaseHeight);
    }

    private static SKColor[] CropFeature(SKBitmap source, MaaRoi roi)
    {
        using var crop = new SKBitmap(roi.Width, roi.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(crop))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(
                source,
                new SKRect(roi.X, roi.Y, roi.X + roi.Width, roi.Y + roi.Height),
                new SKRect(0, 0, roi.Width, roi.Height));
        }
        using var resized = Resize(crop, FeatureSize, FeatureSize);
        var pixels = new SKColor[FeatureSize * FeatureSize];
        var index = 0;
        for (var y = 0; y < FeatureSize; y++)
        {
            for (var x = 0; x < FeatureSize; x++)
                pixels[index++] = resized.GetPixel(x, y);
        }
        return pixels;
    }

    private static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        return source.Resize(
                new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
    }

    private static double Similarity(IReadOnlyList<SKColor> actual, CoinTemplate template)
    {
        double difference = 0;
        double weight = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            var expected = template.Pixels[index];
            if (expected.Alpha < 128)
                continue;

            var alpha = expected.Alpha / 255d;
            var color = actual[index];
            difference += alpha * (
                Math.Abs(color.Red - expected.Red)
                + Math.Abs(color.Green - expected.Green)
                + Math.Abs(color.Blue - expected.Blue));
            weight += alpha;
        }

        return weight <= 0 ? 0 : 1 - (difference / (weight * 3 * 255));
    }

    private static RhodesSuiCoinImageDetection? ReadDetection(JsonElement item)
    {
        var coinId = StringProperty(item, "coinId");
        if (string.IsNullOrWhiteSpace(coinId))
            return null;

        var roi = item.TryGetProperty("roi", out var roiProperty)
            && roiProperty.ValueKind == JsonValueKind.Array
            ? roiProperty.EnumerateArray().Select(value => value.GetInt32()).ToArray()
            : [];
        return new RhodesSuiCoinImageDetection(
            coinId,
            StringProperty(item, "label"),
            NumberProperty(item, "score"),
            IntProperty(item, "slotIndex"),
            roi.Length == 4 ? new MaaRoi(roi[0], roi[1], roi[2], roi[3]) : new MaaRoi(0, 0, 0, 0),
            StringProperty(item, "statusId"));
    }

    private static string StringProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(propertyName, out var property)
        && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? ""
            : "";

    private static double NumberProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(propertyName, out var property)
        && property.TryGetDouble(out var value)
            ? value
            : 0;

    private static int IntProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(propertyName, out var property)
        && property.TryGetInt32(out var value)
            ? value
            : 0;

    private sealed record CoinTemplate(SukiSpecialEffectOption Option, IReadOnlyList<TemplatePixel> Pixels);

    private sealed record TemplatePixel(byte Red, byte Green, byte Blue, byte Alpha);

    private sealed record SlotMatch(CoinTemplate Template, double Score, double RunnerUpScore, MaaRoi Roi);
}
