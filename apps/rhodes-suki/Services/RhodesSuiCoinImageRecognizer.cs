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
    string StatusId = "",
    double RunnerUpScore = 0,
    double VisualStrength = 0);

public sealed record RhodesSuiOwnedCoinRecognition(
    MaaTaskRunResult ImageResult,
    IReadOnlyList<MaaDynamicOcrRequest> NameOcrRequests);

public static class RhodesSuiCoinImageRecognizer
{
    public const string ActiveEntry = "RhodesSuiCoinImage_activeCoins";
    public const string ActiveFieldId = "activeCoins";
    public const string OwnedEntry = "RhodesSuiCoinImage_ownedCoins";
    public const string OwnedFieldId = "coins";
    public const string Entry = ActiveEntry;
    public const string FieldId = ActiveFieldId;

    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const int FeatureSize = 32;
    private const double MinimumScore = 0.925;
    private const double MinimumMargin = 0.05;
    private const double StrongScore = 0.945;
    private const double StrongMinimumMargin = 0.025;
    private const double ActiveOcrPresenceVisualFloor = 0.84;
    private const double OwnedMinimumScore = 0.77;
    private const double OwnedMinimumMargin = 0.012;
    private const double OwnedMinimumVisualStrength = 0.70;
    private const double OwnedOcrMinimumVisualStrength = 0.68;
    private const double OwnedCoarseVisualFloor = 0.60;
    private const double OwnedOcrPresenceVisualFloor = 0.48;
    private const int OwnedNameWidth = 200;
    private const int OwnedNameHeight = 30;
    private const int OwnedNameOffsetY = 47;
    private const int CoinListRoiX = 120;
    private const int CoinListRoiY = 96;
    private const double CoinListOcrScale = 2;
    private const int MaximumMissingNameOcrRequests = 4;
    private const int OwnedShortlistSize = 12;

    private static readonly ActivePanelSlot[] ActivePanelSlots =
    [
        new(0, 532, 207, 620, 180, 470, 130),
        new(1, 532, 327, 620, 305, 470, 145),
        new(2, 532, 467, 620, 445, 470, 165),
    ];
    private static readonly int[] ActivePanelSizes = [96, 100, 104];
    private static readonly int[] ActivePanelOffsets = [-4, 0, 4];
    private static readonly int[] OwnedCoarseSizes = [106, 118, 132];
    private static readonly int[] OwnedCoarseXOffsets = [-6, 0, 6];
    private static readonly int[] OwnedFineSizes = [100, 106, 112, 118, 124, 128, 132, 136];
    private static readonly OwnedSlot[] OwnedSlots =
    [
        new(0, 601, 211),
        new(1, 881, 211),
        new(2, 1143, 211),
        new(3, 463, 348),
        new(4, 742, 348),
        new(5, 1005, 348),
        new(6, 601, 486),
        new(7, 881, 486),
        new(8, 1143, 486),
    ];
    private static readonly Lazy<IReadOnlyList<CoinTemplate>> DefaultTemplates = new(BuildDefaultTemplates);

    public static MaaTaskRunResult Recognize(
        byte[] encodedImage,
        IReadOnlyList<SukiSpecialEffectOption>? coinOptions = null)
    {
        var detections = Detect(encodedImage, coinOptions);
        return BuildResult(ActiveEntry, ActiveFieldId, "activeCoins", detections);
    }

    public static MaaTaskRunResult RecognizeOwned(
        byte[] encodedImage,
        IReadOnlyList<SukiSpecialEffectOption>? coinOptions = null)
    {
        return RecognizeOwnedWithOcrFallback(encodedImage, coinOptions).ImageResult;
    }

    internal static MaaTaskRunResult CreateOwnedResult(
        IReadOnlyList<RhodesSuiCoinImageDetection> detections) =>
        BuildResult(OwnedEntry, OwnedFieldId, "ownedCoins", detections);

    public static RhodesSuiOwnedCoinRecognition RecognizeOwnedWithOcrFallback(
        byte[] encodedImage,
        IReadOnlyList<SukiSpecialEffectOption>? coinOptions = null)
    {
        var inspections = InspectOwned(encodedImage, coinOptions);
        var detections = inspections.Where(IsConfidentOwnedMatch).ToArray();
        return new RhodesSuiOwnedCoinRecognition(
            BuildResult(OwnedEntry, OwnedFieldId, "ownedCoins", detections),
            PlanOwnedNameOcrRequests(inspections));
    }

    private static MaaTaskRunResult BuildResult(
        string entry,
        string fieldId,
        string detailLabel,
        IReadOnlyList<RhodesSuiCoinImageDetection> detections)
    {
        var detail = JsonSerializer.Serialize(new
        {
            fieldId,
            detections = detections.Select(detection => new
            {
                coinId = detection.CoinId,
                label = detection.Label,
                score = detection.Score,
                slotIndex = detection.SlotIndex,
                roi = detection.Roi.ToArray(),
                statusId = detection.StatusId,
                runnerUpScore = detection.RunnerUpScore,
                visualStrength = detection.VisualStrength,
            }),
        });
        return new MaaTaskRunResult(
            entry,
            "Succeeded",
            true,
            $"{detailLabel}={detections.Count}",
            detail,
            "ImageClassification",
            detections.Count > 0);
    }

    public static IReadOnlyList<RhodesSuiCoinImageDetection> Detect(
        byte[] encodedImage,
        IReadOnlyList<SukiSpecialEffectOption>? coinOptions = null)
    {
        return InspectActive(encodedImage, coinOptions)
            .Where(IsConfidentActiveMatch)
            .ToArray();
    }

    public static IReadOnlyList<RhodesSuiCoinImageDetection> InspectActive(
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
        foreach (var slot in ActivePanelSlots)
        {
            var match = BestSlotMatch(normalized, templates, slot);
            if (match is null)
                continue;

            detections.Add(new RhodesSuiCoinImageDetection(
                match.Template.Option.Id,
                match.Template.Option.Name,
                match.Score,
                slot.Index,
                match.Roi,
                RunnerUpScore: match.RunnerUpScore,
                VisualStrength: match.VisualStrength));
        }
        return detections;
    }

    public static IReadOnlyList<RhodesSuiCoinImageDetection> DetectOwned(
        byte[] encodedImage,
        IReadOnlyList<SukiSpecialEffectOption>? coinOptions = null)
    {
        return InspectOwned(encodedImage, coinOptions)
            .Where(IsConfidentOwnedMatch)
            .ToArray();
    }

    public static IReadOnlyList<MaaDynamicOcrRequest> PlanActivePanelOcrRequests(
        IReadOnlyList<RhodesSuiCoinImageDetection> inspections)
    {
        var visibleSlots = inspections
            .Where(detection => detection.VisualStrength >= ActiveOcrPresenceVisualFloor)
            .Select(detection => detection.SlotIndex)
            .ToHashSet();
        return ActivePanelSlots
            .Where(slot => visibleSlots.Contains(slot.Index))
            .Select(slot => new MaaDynamicOcrRequest(
                $"RhodesDynamic_is6.active_coin_list_text.slot{slot.Index}",
                slot.OcrX,
                slot.OcrY,
                slot.OcrWidth,
                slot.OcrHeight,
                2,
                inspections
                    .Where(detection => detection.SlotIndex == slot.Index)
                    .Select(detection => detection.VisualStrength)
                    .DefaultIfEmpty(0)
                    .Max(),
                OnlyRecognition: false))
            .ToArray();
    }

    public static IReadOnlyList<MaaDynamicOcrRequest> PlanOwnedNameOcrRequests(
        IReadOnlyList<RhodesSuiCoinImageDetection> inspections)
    {
        var resolvedSlots = inspections
            .Where(IsConfidentOwnedMatch)
            .Select(detection => detection.SlotIndex)
            .ToHashSet();
        var requests = new List<MaaDynamicOcrRequest>();
        foreach (var inspection in inspections
                     .Where(detection => detection.VisualStrength >= OwnedOcrMinimumVisualStrength)
                     .Where(detection => !resolvedSlots.Contains(detection.SlotIndex))
                     .OrderBy(detection => detection.SlotIndex))
        {
            var slot = OwnedSlots.FirstOrDefault(item => item.Index == inspection.SlotIndex);
            if (slot is null)
                continue;

            var x = Math.Clamp(slot.CenterX - (OwnedNameWidth / 2), 0, BaseWidth - OwnedNameWidth);
            var y = Math.Clamp(slot.CenterY + OwnedNameOffsetY, 0, BaseHeight - OwnedNameHeight);
            requests.Add(new MaaDynamicOcrRequest(
                $"RhodesDynamic_is6.coin_list_text.slot{slot.Index}",
                x,
                y,
                OwnedNameWidth,
                OwnedNameHeight,
                3,
                inspection.VisualStrength));
        }
        return requests;
    }

    public static IReadOnlyList<MaaDynamicOcrRequest> PlanMissingOwnedNameOcrRequests(
        IReadOnlyList<RhodesSuiCoinImageDetection> inspections,
        IEnumerable<MaaTaskRunResult> frameTaskResults)
    {
        var resolvedSlots = frameTaskResults
            .Where(result => result.Entry.Equals("RhodesOcrRegion_is6_coin_list_text", StringComparison.Ordinal))
            .SelectMany(RhodesMaaLocalCandidateConverter.ResolveSuiCoinOcrMatches)
            .Select(match => MatchOwnedSlot(match.OcrBox))
            .Where(slotIndex => slotIndex is not null)
            .Select(slotIndex => slotIndex!.Value)
            .ToHashSet();

        return inspections
            .Where(detection => detection.VisualStrength >= OwnedOcrPresenceVisualFloor)
            .Where(detection => !resolvedSlots.Contains(detection.SlotIndex))
            .GroupBy(detection => detection.SlotIndex)
            .Select(group => group.OrderByDescending(detection => detection.VisualStrength).First())
            .OrderByDescending(detection => detection.VisualStrength)
            .ThenBy(detection => detection.SlotIndex)
            .Take(MaximumMissingNameOcrRequests)
            .Select(detection => BuildOwnedNameOcrRequest(detection.SlotIndex, detection.VisualStrength))
            .Where(request => request is not null)
            .Cast<MaaDynamicOcrRequest>()
            .ToArray();
    }

    private static MaaDynamicOcrRequest? BuildOwnedNameOcrRequest(int slotIndex, double visualStrength)
    {
        var slot = OwnedSlots.FirstOrDefault(item => item.Index == slotIndex);
        if (slot is null)
            return null;

        var x = Math.Clamp(slot.CenterX - (OwnedNameWidth / 2), 0, BaseWidth - OwnedNameWidth);
        var y = Math.Clamp(slot.CenterY + OwnedNameOffsetY, 0, BaseHeight - OwnedNameHeight);
        return new MaaDynamicOcrRequest(
            $"RhodesDynamic_is6.coin_list_text.slot{slot.Index}",
            x,
            y,
            OwnedNameWidth,
            OwnedNameHeight,
            3,
            visualStrength);
    }

    internal static int? MatchOwnedSlot(MaaRoi ocrBox)
    {
        var screenLeft = CoinListRoiX + (ocrBox.X / CoinListOcrScale);
        var screenCenterY = CoinListRoiY + ((ocrBox.Y + (ocrBox.Height / 2d)) / CoinListOcrScale);
        var nearest = OwnedSlots
            .Select(slot => new
            {
                Slot = slot,
                RowDistance = Math.Abs((slot.CenterY + OwnedNameOffsetY + (OwnedNameHeight / 2d)) - screenCenterY),
                ColumnDistance = Math.Abs((slot.CenterX - (OwnedNameWidth / 2d)) - screenLeft),
            })
            .Where(item => item.RowDistance <= 42 && item.ColumnDistance <= 130)
            .OrderBy(item => item.RowDistance)
            .ThenBy(item => item.ColumnDistance)
            .FirstOrDefault();
        return nearest?.Slot.Index;
    }

    public static IReadOnlyList<RhodesSuiCoinImageDetection> InspectOwned(
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
        foreach (var slot in OwnedSlots)
        {
            var match = BestOwnedSlotMatch(normalized, templates, slot);
            if (match is null)
                continue;

            detections.Add(new RhodesSuiCoinImageDetection(
                match.Template.Option.Id,
                match.Template.Option.Name,
                match.Score,
                slot.Index,
                match.Roi,
                RunnerUpScore: match.RunnerUpScore,
                VisualStrength: match.VisualStrength));
        }
        return detections;
    }

    private static bool IsConfidentActiveMatch(RhodesSuiCoinImageDetection detection)
    {
        if (detection.Score < MinimumScore)
            return false;

        var margin = detection.Score - detection.RunnerUpScore;
        return margin >= MinimumMargin
            || (detection.Score >= StrongScore && margin >= StrongMinimumMargin);
    }

    private static bool IsConfidentOwnedMatch(RhodesSuiCoinImageDetection detection) =>
        detection.Score >= OwnedMinimumScore
        && detection.Score - detection.RunnerUpScore >= OwnedMinimumMargin
        && detection.VisualStrength >= OwnedMinimumVisualStrength;

    public static bool TryRead(
        MaaTaskRunResult taskResult,
        out string fieldId,
        out IReadOnlyList<RhodesSuiCoinImageDetection> detections)
    {
        fieldId = "";
        detections = [];
        var expectedFieldId = taskResult.Entry switch
        {
            ActiveEntry => ActiveFieldId,
            OwnedEntry => OwnedFieldId,
            _ => "",
        };
        if (!taskResult.Succeeded
            || string.IsNullOrWhiteSpace(expectedFieldId)
            || string.IsNullOrWhiteSpace(taskResult.RecognitionDetailJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(taskResult.RecognitionDetailJson);
            var root = document.RootElement;
            fieldId = StringProperty(root, "fieldId");
            if (!fieldId.Equals(expectedFieldId, StringComparison.Ordinal)
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
        ActivePanelSlot slot)
    {
        var bestByCoin = new Dictionary<string, SlotMatch>(StringComparer.Ordinal);
        foreach (var xOffset in ActivePanelOffsets)
        {
            foreach (var yOffset in ActivePanelOffsets)
            {
                foreach (var size in ActivePanelSizes)
                {
                    var roi = new MaaRoi(slot.X + xOffset, slot.Y + yOffset, size, size);
                    if (!IsInside(frame, roi))
                        continue;
                    var pixels = CropFeature(frame, roi);
                    foreach (var template in templates)
                    {
                        var (score, visualStrength) = OwnedSimilarity(pixels, template);
                        if (!bestByCoin.TryGetValue(template.Option.Id, out var current)
                            || score > current.Score)
                        {
                            bestByCoin[template.Option.Id] = new SlotMatch(
                                template,
                                score,
                                0,
                                roi,
                                visualStrength);
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

    private static SlotMatch? BestOwnedSlotMatch(
        SKBitmap frame,
        IReadOnlyList<CoinTemplate> templates,
        OwnedSlot slot)
    {
        var coarseBestByCoin = new Dictionary<string, SlotMatch>(StringComparer.Ordinal);
        foreach (var size in OwnedCoarseSizes)
        {
            foreach (var xOffset in OwnedCoarseXOffsets)
            {
                var roi = OwnedRoi(slot, size, xOffset, 0);
                if (!IsInside(frame, roi))
                    continue;

                var pixels = CropFeature(frame, roi);
                foreach (var template in templates)
                {
                    var (score, visualStrength) = OwnedSimilarity(pixels, template);
                    if (!coarseBestByCoin.TryGetValue(template.Option.Id, out var current)
                        || score > current.Score)
                    {
                        coarseBestByCoin[template.Option.Id] = new SlotMatch(template, score, 0, roi, visualStrength);
                    }
                }
            }
        }

        var shortlistedIds = coarseBestByCoin.Values
            .OrderByDescending(match => match.Score)
            .Take(OwnedShortlistSize)
            .Select(match => match.Template.Option.Id)
            .ToHashSet(StringComparer.Ordinal);
        var shortlistedTemplates = templates
            .Where(template => shortlistedIds.Contains(template.Option.Id))
            .ToArray();
        if (shortlistedTemplates.Length == 0)
            return null;

        var coarseRanked = coarseBestByCoin.Values
            .OrderByDescending(match => match.Score)
            .Take(2)
            .ToArray();
        if (coarseBestByCoin.Values.Max(match => match.VisualStrength) < OwnedCoarseVisualFloor)
        {
            return coarseRanked[0] with
            {
                RunnerUpScore = coarseRanked.ElementAtOrDefault(1)?.Score ?? 0,
            };
        }

        var bestByCoin = new Dictionary<string, SlotMatch>(coarseBestByCoin, StringComparer.Ordinal);
        foreach (var size in OwnedFineSizes)
        {
            for (var xOffset = -6; xOffset <= 6; xOffset += 6)
            {
                for (var yOffset = -4; yOffset <= 4; yOffset += 4)
                {
                    if (yOffset == 0
                        && OwnedCoarseSizes.Contains(size)
                        && OwnedCoarseXOffsets.Contains(xOffset))
                    {
                        continue;
                    }

                    var roi = OwnedRoi(slot, size, xOffset, yOffset);
                    if (!IsInside(frame, roi))
                        continue;

                    var pixels = CropFeature(frame, roi);
                    foreach (var template in shortlistedTemplates)
                    {
                        var (score, visualStrength) = OwnedSimilarity(pixels, template);
                        if (!bestByCoin.TryGetValue(template.Option.Id, out var current)
                            || score > current.Score)
                        {
                            bestByCoin[template.Option.Id] = new SlotMatch(template, score, 0, roi, visualStrength);
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

    private static MaaRoi OwnedRoi(OwnedSlot slot, int size, int xOffset, int yOffset) =>
        new(
            slot.CenterX - (size / 2) + xOffset,
            slot.CenterY - (size / 2) + yOffset,
            size,
            size);

    private static bool IsInside(SKBitmap frame, MaaRoi roi) =>
        roi.X >= 0
        && roi.Y >= 0
        && roi.X + roi.Width <= frame.Width
        && roi.Y + roi.Height <= frame.Height;

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
            templates.Add(new CoinTemplate(option, pixels, BuildOwnedTemplateStats(pixels)));
        }
        return templates;
    }

    private static OwnedTemplateStats BuildOwnedTemplateStats(IReadOnlyList<TemplatePixel> pixels)
    {
        var histogram = new double[64];
        var distinctiveWeights = new double[pixels.Count];
        var channelSums = new double[3];
        var channelSquareSums = new double[3];
        double weight = 0;
        double distinctiveWeight = 0;
        double luminanceSum = 0;
        double luminanceSquareSum = 0;
        double chromaSum = 0;

        for (var index = 0; index < pixels.Count; index++)
        {
            if (IsOwnedOverlayPixel(index))
                continue;
            var pixel = pixels[index];
            if (pixel.Alpha < 128)
                continue;

            var alpha = pixel.Alpha / 255d;
            var chroma = Chroma(pixel.Red, pixel.Green, pixel.Blue);
            var luminance = Luminance(pixel.Red, pixel.Green, pixel.Blue);
            var distinctive = alpha * (1 + ((chroma / 255d) * 5));
            distinctiveWeights[index] = distinctive;
            histogram[OwnedColorBin(pixel.Red, pixel.Green, pixel.Blue)] += alpha;
            weight += alpha;
            distinctiveWeight += distinctive;
            luminanceSum += alpha * luminance;
            luminanceSquareSum += alpha * luminance * luminance;
            chromaSum += alpha * chroma;
            for (var channel = 0; channel < 3; channel++)
            {
                var value = Channel(pixel.Red, pixel.Green, pixel.Blue, channel);
                channelSums[channel] += alpha * value;
                channelSquareSums[channel] += alpha * value * value;
            }
        }

        var luminanceMean = weight <= 0 ? 0 : luminanceSum / weight;
        var channelMeans = channelSums.Select(sum => weight <= 0 ? 0 : sum / weight).ToArray();
        return new OwnedTemplateStats(
            weight,
            distinctiveWeight,
            distinctiveWeights,
            histogram,
            luminanceMean,
            Math.Max(0, luminanceSquareSum - (weight * luminanceMean * luminanceMean)),
            weight <= 0 ? 0 : chromaSum / weight,
            channelMeans,
            channelSquareSums
                .Select((sum, channel) => Math.Max(0, sum - (weight * channelMeans[channel] * channelMeans[channel])))
                .ToArray());
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

    private static double Similarity(
        IReadOnlyList<SKColor> actual,
        CoinTemplate template,
        bool ignoreOwnedOverlay = false)
    {
        double difference = 0;
        double weight = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            if (ignoreOwnedOverlay && IsOwnedOverlayPixel(index))
                continue;
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

    private static (double Score, double VisualStrength) OwnedSimilarity(
        IReadOnlyList<SKColor> actual,
        CoinTemplate template)
    {
        var stats = template.OwnedStats;
        if (stats.Weight <= 0)
            return (0, 0);

        Span<double> actualHistogram = stackalloc double[64];
        Span<double> actualChannelSums = stackalloc double[3];
        Span<double> actualChannelSquareSums = stackalloc double[3];
        Span<double> channelProducts = stackalloc double[3];
        double absoluteDifference = 0;
        double distinctiveDifference = 0;
        double actualLuminanceSum = 0;
        double actualLuminanceSquareSum = 0;
        double actualChromaSum = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            if (IsOwnedOverlayPixel(index))
                continue;
            var expected = template.Pixels[index];
            if (expected.Alpha < 128)
                continue;

            var alpha = expected.Alpha / 255d;
            var color = actual[index];
            absoluteDifference += alpha * (
                Math.Abs(color.Red - expected.Red)
                + Math.Abs(color.Green - expected.Green)
                + Math.Abs(color.Blue - expected.Blue));
            distinctiveDifference += stats.DistinctiveWeights[index] * (
                Math.Abs(color.Red - expected.Red)
                + Math.Abs(color.Green - expected.Green)
                + Math.Abs(color.Blue - expected.Blue));
            actualHistogram[OwnedColorBin(color.Red, color.Green, color.Blue)] += alpha;

            var actualLuminance = Luminance(color.Red, color.Green, color.Blue);
            actualLuminanceSum += alpha * actualLuminance;
            actualLuminanceSquareSum += alpha * actualLuminance * actualLuminance;
            actualChromaSum += alpha * Chroma(color.Red, color.Green, color.Blue);
            for (var channel = 0; channel < 3; channel++)
            {
                var expectedValue = Channel(expected.Red, expected.Green, expected.Blue, channel);
                var actualValue = Channel(color.Red, color.Green, color.Blue, channel);
                actualChannelSums[channel] += alpha * actualValue;
                actualChannelSquareSums[channel] += alpha * actualValue * actualValue;
                channelProducts[channel] += alpha * expectedValue * actualValue;
            }
        }

        var absolute = 1 - (absoluteDifference / (stats.Weight * 3 * 255));
        var distinctive = stats.DistinctiveWeight <= 0
            ? 0
            : 1 - (distinctiveDifference / (stats.DistinctiveWeight * 3 * 255));
        double colorProfile = 0;
        for (var index = 0; index < actualHistogram.Length; index++)
            colorProfile += Math.Min(stats.Histogram[index], actualHistogram[index]) / stats.Weight;

        double channelCorrelation = 0;
        for (var channel = 0; channel < 3; channel++)
        {
            var actualMean = actualChannelSums[channel] / stats.Weight;
            var covariance = channelProducts[channel] - (stats.ChannelMeans[channel] * actualChannelSums[channel]);
            var actualVariance = Math.Max(0, actualChannelSquareSums[channel] - (stats.Weight * actualMean * actualMean));
            var denominator = Math.Sqrt(stats.ChannelVariances[channel] * actualVariance);
            channelCorrelation += denominator <= 0 ? 0 : Math.Max(0, covariance / denominator);
        }
        var correlation = channelCorrelation / 3d;

        var actualLuminanceMean = actualLuminanceSum / stats.Weight;
        var actualLuminanceVariance = Math.Max(
            0,
            actualLuminanceSquareSum - (stats.Weight * actualLuminanceMean * actualLuminanceMean));
        var actualChromaMean = actualChromaSum / stats.Weight;
        var contrastRatio = stats.LuminanceVariance <= 0
            ? 0
            : Math.Sqrt(actualLuminanceVariance / stats.LuminanceVariance);
        var chromaRatio = stats.ChromaMean <= 0
            ? 0
            : actualChromaMean / stats.ChromaMean;
        var visualStrength = Math.Clamp((contrastRatio + chromaRatio) / 2d, 0, 1);
        return ((correlation * 0.10)
            + (absolute * 0.10)
            + (distinctive * 0.35)
            + (colorProfile * 0.45), visualStrength);
    }

    private static int OwnedColorBin(byte red, byte green, byte blue)
    {
        const int binsPerChannel = 4;
        var redBin = Math.Min(binsPerChannel - 1, red * binsPerChannel / 256);
        var greenBin = Math.Min(binsPerChannel - 1, green * binsPerChannel / 256);
        var blueBin = Math.Min(binsPerChannel - 1, blue * binsPerChannel / 256);
        return (redBin * binsPerChannel * binsPerChannel) + (greenBin * binsPerChannel) + blueBin;
    }

    private static double ColorProfileSimilarity(
        IReadOnlyList<SKColor> actual,
        CoinTemplate template)
    {
        const int binsPerChannel = 4;
        const int binCount = binsPerChannel * binsPerChannel * binsPerChannel;
        var expectedHistogram = new double[binCount];
        var actualHistogram = new double[binCount];
        double expectedWeight = 0;
        double actualWeight = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            if (IsOwnedOverlayPixel(index))
                continue;
            var expected = template.Pixels[index];
            if (expected.Alpha < 128)
                continue;

            var alpha = expected.Alpha / 255d;
            var color = actual[index];
            expectedHistogram[ColorBin(expected.Red, expected.Green, expected.Blue)] += alpha;
            actualHistogram[ColorBin(color.Red, color.Green, color.Blue)] += alpha;
            expectedWeight += alpha;
            actualWeight += alpha;
        }

        if (expectedWeight <= 0 || actualWeight <= 0)
            return 0;

        double intersection = 0;
        for (var index = 0; index < binCount; index++)
        {
            intersection += Math.Min(
                expectedHistogram[index] / expectedWeight,
                actualHistogram[index] / actualWeight);
        }
        return intersection;

        static int ColorBin(byte red, byte green, byte blue)
        {
            var redBin = Math.Min(binsPerChannel - 1, red * binsPerChannel / 256);
            var greenBin = Math.Min(binsPerChannel - 1, green * binsPerChannel / 256);
            var blueBin = Math.Min(binsPerChannel - 1, blue * binsPerChannel / 256);
            return (redBin * binsPerChannel * binsPerChannel) + (greenBin * binsPerChannel) + blueBin;
        }
    }

    private static double DistinctiveColorSimilarity(
        IReadOnlyList<SKColor> actual,
        CoinTemplate template)
    {
        double difference = 0;
        double weight = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            if (IsOwnedOverlayPixel(index))
                continue;
            var expected = template.Pixels[index];
            if (expected.Alpha < 128)
                continue;

            var alpha = expected.Alpha / 255d;
            var chroma = Chroma(expected.Red, expected.Green, expected.Blue) / 255d;
            var pixelWeight = alpha * (1 + (chroma * 5));
            var color = actual[index];
            difference += pixelWeight * (
                Math.Abs(color.Red - expected.Red)
                + Math.Abs(color.Green - expected.Green)
                + Math.Abs(color.Blue - expected.Blue));
            weight += pixelWeight;
        }

        return weight <= 0 ? 0 : 1 - (difference / (weight * 3 * 255));
    }

    private static double ChannelCorrelation(
        IReadOnlyList<SKColor> actual,
        CoinTemplate template,
        int channel)
    {
        double weight = 0;
        double expectedMean = 0;
        double actualMean = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            if (IsOwnedOverlayPixel(index))
                continue;
            var expected = template.Pixels[index];
            if (expected.Alpha < 128)
                continue;

            var alpha = expected.Alpha / 255d;
            expectedMean += alpha * Channel(expected.Red, expected.Green, expected.Blue, channel);
            var color = actual[index];
            actualMean += alpha * Channel(color.Red, color.Green, color.Blue, channel);
            weight += alpha;
        }
        if (weight <= 0)
            return 0;
        expectedMean /= weight;
        actualMean /= weight;

        double covariance = 0;
        double expectedVariance = 0;
        double actualVariance = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            if (IsOwnedOverlayPixel(index))
                continue;
            var expected = template.Pixels[index];
            if (expected.Alpha < 128)
                continue;

            var alpha = expected.Alpha / 255d;
            var expectedDelta = Channel(expected.Red, expected.Green, expected.Blue, channel) - expectedMean;
            var color = actual[index];
            var actualDelta = Channel(color.Red, color.Green, color.Blue, channel) - actualMean;
            covariance += alpha * expectedDelta * actualDelta;
            expectedVariance += alpha * expectedDelta * expectedDelta;
            actualVariance += alpha * actualDelta * actualDelta;
        }

        var denominator = Math.Sqrt(expectedVariance * actualVariance);
        return denominator <= 0 ? 0 : Math.Max(0, covariance / denominator);
    }

    private static bool IsOwnedOverlayPixel(int index)
    {
        var x = index % FeatureSize;
        var y = index / FeatureSize;
        return x >= 20 && y <= 14;
    }

    private static double Channel(byte red, byte green, byte blue, int channel) =>
        channel switch
        {
            0 => red,
            1 => green,
            _ => blue,
        };

    private static double Luminance(byte red, byte green, byte blue) =>
        (red * 0.2126) + (green * 0.7152) + (blue * 0.0722);

    private static int Chroma(byte red, byte green, byte blue) =>
        Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));

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

    private sealed record CoinTemplate(
        SukiSpecialEffectOption Option,
        IReadOnlyList<TemplatePixel> Pixels,
        OwnedTemplateStats OwnedStats);

    private sealed record OwnedTemplateStats(
        double Weight,
        double DistinctiveWeight,
        IReadOnlyList<double> DistinctiveWeights,
        IReadOnlyList<double> Histogram,
        double LuminanceMean,
        double LuminanceVariance,
        double ChromaMean,
        IReadOnlyList<double> ChannelMeans,
        IReadOnlyList<double> ChannelVariances);

    private sealed record TemplatePixel(byte Red, byte Green, byte Blue, byte Alpha);

    private sealed record SlotMatch(
        CoinTemplate Template,
        double Score,
        double RunnerUpScore,
        MaaRoi Roi,
        double VisualStrength = 1);

    private sealed record OwnedSlot(int Index, int CenterX, int CenterY);

    private sealed record ActivePanelSlot(
        int Index,
        int X,
        int Y,
        int OcrX,
        int OcrY,
        int OcrWidth,
        int OcrHeight);
}
