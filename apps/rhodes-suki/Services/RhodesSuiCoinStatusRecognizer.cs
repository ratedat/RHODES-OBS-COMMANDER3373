using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

public static class RhodesSuiCoinStatusRecognizer
{
    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const int FeatureSize = 32;
    private const int CoinListRoiX = 120;
    private const int CoinListRoiY = 96;
    private const double CoinListOcrScale = 2;
    private const double CoinCenterOffsetFromText = 62;
    private const int ExpectedStatusOffsetX = 12;
    private const int ExpectedStatusOffsetY = -46;
    private const double MinimumScore = 0.82;
    private const double AmbiguousGild5MinimumScore = 0.90;
    private const double MinimumMargin = 0.01;
    private const double MinimumOverlayDifference = 0.035;
    private static readonly int[] StatusTemplateWidths = [40, 44, 48];
    private static readonly int[] CoinTemplateSizes = [100, 106, 112];
    private static readonly Lazy<IReadOnlyList<StatusTemplate>> DefaultStatusTemplates =
        new(() => BuildStatusTemplates(RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coinStatus")));
    private static readonly Lazy<IReadOnlyDictionary<string, CoinBaselineTemplate>> DefaultCoinTemplates =
        new(() => BuildCoinTemplates(RhodesRunCatalog.LoadSpecialEffectOptions("is6_sui", "coin")));

    public static MaaTaskRunResult RecognizeOwned(
        byte[] encodedImage,
        IEnumerable<MaaTaskRunResult> frameTaskResults,
        IReadOnlyList<SukiSpecialEffectOption>? coinOptions = null,
        IReadOnlyList<SukiSpecialEffectOption>? statusOptions = null)
    {
        if (encodedImage.Length == 0)
            return RhodesSuiCoinImageRecognizer.CreateOwnedResult([]);

        using var decoded = SKBitmap.Decode(encodedImage);
        if (decoded is null || decoded.Width <= 0 || decoded.Height <= 0)
            return RhodesSuiCoinImageRecognizer.CreateOwnedResult([]);

        using var normalized = NormalizeFrame(decoded);
        var templates = statusOptions is null
            ? DefaultStatusTemplates.Value
            : BuildStatusTemplates(statusOptions);
        var coinTemplates = coinOptions is null
            ? DefaultCoinTemplates.Value
            : BuildCoinTemplates(coinOptions);
        var matches = frameTaskResults
            .Where(result => result.Entry.Equals("RhodesOcrRegion_is6_coin_list_text", StringComparison.Ordinal))
            .SelectMany(RhodesMaaLocalCandidateConverter.ResolveSuiCoinOcrMatches)
            .OrderBy(match => ScreenTextCenterY(match.OcrBox))
            .ThenBy(match => ScreenTextCenterX(match.OcrBox))
            .ToArray();
        if (matches.Length == 0)
            return RhodesSuiCoinImageRecognizer.CreateOwnedResult([]);

        var detections = new List<RhodesSuiCoinImageDetection>(matches.Length);
        for (var index = 0; index < matches.Length; index++)
        {
            var match = matches[index];
            var centerX = ScreenTextCenterX(match.OcrBox);
            var centerY = ScreenTextCenterY(match.OcrBox) - CoinCenterOffsetFromText;
            var status = BestStatusMatch(normalized, templates, centerX, centerY);
            var overlayDifference = coinTemplates.TryGetValue(match.CoinId, out var coinTemplate)
                ? StatusOverlayDifference(normalized, coinTemplate, centerX, centerY)
                : 0;
            var statusId = overlayDifference >= MinimumOverlayDifference
                && status is not null
                && status.Score >= MinimumStatusScore(status.Template.Option.Id)
                && status.Score - status.RunnerUpScore >= MinimumMargin
                    ? status.Template.Option.Id
                    : "";
            var evidenceRoi = statusId.Length > 0
                ? status!.Roi
                : CoinRoi(centerX, centerY);
            detections.Add(new RhodesSuiCoinImageDetection(
                match.CoinId,
                match.Label,
                statusId.Length > 0 ? Math.Min(match.Confidence, status!.Score) : match.Confidence,
                index,
                evidenceRoi,
                statusId,
                status?.RunnerUpScore ?? 0,
                overlayDifference));
        }

        return RhodesSuiCoinImageRecognizer.CreateOwnedResult(detections);
    }

    private static StatusMatch? BestStatusMatch(
        SKBitmap frame,
        IReadOnlyList<StatusTemplate> templates,
        double coinCenterX,
        double coinCenterY)
    {
        if (templates.Count == 0)
            return null;

        var expectedX = (int)Math.Round(coinCenterX) + ExpectedStatusOffsetX;
        var expectedY = (int)Math.Round(coinCenterY) + ExpectedStatusOffsetY;
        var bestByStatus = new Dictionary<string, StatusMatch>(StringComparer.Ordinal);
        foreach (var template in templates)
        {
            for (var y = expectedY - 6; y <= expectedY + 6; y += 2)
            {
                for (var x = expectedX - 10; x <= expectedX + 10; x += 2)
                {
                    var roi = new MaaRoi(x, y, template.Width, template.Height);
                    if (!IsInside(frame, roi))
                        continue;

                    var score = Similarity(frame, roi, template);
                    if (!bestByStatus.TryGetValue(template.Option.Id, out var current)
                        || score > current.Score)
                    {
                        bestByStatus[template.Option.Id] = new StatusMatch(template, score, 0, roi);
                    }
                }
            }
        }

        var ranked = bestByStatus.Values
            .OrderByDescending(match => match.Score)
            .Take(2)
            .ToArray();
        return ranked.Length == 0
            ? null
            : ranked[0] with { RunnerUpScore = ranked.ElementAtOrDefault(1)?.Score ?? 0 };
    }

    private static double Similarity(SKBitmap frame, MaaRoi roi, StatusTemplate template)
    {
        double difference = 0;
        double weight = 0;
        foreach (var pixel in template.Pixels)
        {
            var actual = frame.GetPixel(roi.X + pixel.X, roi.Y + pixel.Y);
            difference += pixel.Weight * (
                Math.Abs(actual.Red - pixel.Red)
                + Math.Abs(actual.Green - pixel.Green)
                + Math.Abs(actual.Blue - pixel.Blue));
            weight += pixel.Weight;
        }
        return weight <= 0 ? 0 : 1 - (difference / (weight * 3 * 255));
    }

    private static double StatusOverlayDifference(
        SKBitmap frame,
        CoinBaselineTemplate template,
        double centerX,
        double centerY)
    {
        double bestBodyDifference = double.MaxValue;
        double bestOverlayDifference = 0;
        foreach (var size in CoinTemplateSizes)
        {
            for (var yOffset = -4; yOffset <= 4; yOffset += 4)
            {
                for (var xOffset = -4; xOffset <= 4; xOffset += 4)
                {
                    var roi = new MaaRoi(
                        (int)Math.Round(centerX) - (size / 2) + xOffset,
                        (int)Math.Round(centerY) - (size / 2) + yOffset,
                        size,
                        size);
                    if (!IsInside(frame, roi))
                        continue;

                    var (bodyDifference, overlayDifference) = CompareCoinRegions(frame, roi, template);
                    if (bodyDifference < bestBodyDifference)
                    {
                        bestBodyDifference = bodyDifference;
                        bestOverlayDifference = overlayDifference;
                    }
                }
            }
        }

        return bestBodyDifference == double.MaxValue
            ? 0
            : Math.Clamp(bestOverlayDifference, 0, 1);
    }

    private static (double BodyDifference, double OverlayDifference) CompareCoinRegions(
        SKBitmap frame,
        MaaRoi roi,
        CoinBaselineTemplate template)
    {
        using var crop = new SKBitmap(roi.Width, roi.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        using (var canvas = new SKCanvas(crop))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(
                frame,
                new SKRect(roi.X, roi.Y, roi.X + roi.Width, roi.Y + roi.Height),
                new SKRect(0, 0, roi.Width, roi.Height));
        }
        using var resized = Resize(crop, FeatureSize, FeatureSize);
        var actualPixels = new SKColor[FeatureSize * FeatureSize];
        for (var y = 0; y < FeatureSize; y++)
        {
            for (var x = 0; x < FeatureSize; x++)
                actualPixels[(y * FeatureSize) + x] = resized.GetPixel(x, y);
        }
        var redTransform = FitCoinChannel(template, actualPixels, 0);
        var greenTransform = FitCoinChannel(template, actualPixels, 1);
        var blueTransform = FitCoinChannel(template, actualPixels, 2);
        var redBackground = FitBackgroundPlane(template, actualPixels, 0);
        var greenBackground = FitBackgroundPlane(template, actualPixels, 1);
        var blueBackground = FitBackgroundPlane(template, actualPixels, 2);

        double bodyDifference = 0;
        double bodyWeight = 0;
        double overlayDifference = 0;
        double overlayWeight = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            var expected = template.Pixels[index];
            var x = index % FeatureSize;
            var y = index / FeatureSize;
            var actual = actualPixels[index];
            var alpha = expected.Alpha / 255d;
            var expectedRed = (alpha * redTransform.Predict(expected.Red))
                + ((1 - alpha) * redBackground.Predict(x, y));
            var expectedGreen = (alpha * greenTransform.Predict(expected.Green))
                + ((1 - alpha) * greenBackground.Predict(x, y));
            var expectedBlue = (alpha * blueTransform.Predict(expected.Blue))
                + ((1 - alpha) * blueBackground.Predict(x, y));
            var difference = Math.Abs(actual.Red - expectedRed)
                + Math.Abs(actual.Green - expectedGreen)
                + Math.Abs(actual.Blue - expectedBlue);
            if (IsStatusOverlayPixel(x, y))
            {
                overlayDifference += difference;
                overlayWeight++;
            }
            else
            {
                bodyDifference += difference;
                bodyWeight++;
            }
        }

        return (
            bodyWeight <= 0 ? 1 : bodyDifference / (bodyWeight * 3 * 255),
            overlayWeight <= 0 ? 0 : overlayDifference / (overlayWeight * 3 * 255));
    }

    private static bool IsStatusOverlayPixel(int x, int y) => x >= 18 && y <= 17;

    private static double MinimumStatusScore(string statusId) =>
        statusId.EndsWith("is6_gild5", StringComparison.Ordinal)
            ? AmbiguousGild5MinimumScore
            : MinimumScore;

    private static LinearFit FitCoinChannel(
        CoinBaselineTemplate template,
        IReadOnlyList<SKColor> actual,
        int channel)
    {
        double count = 0;
        double expectedSum = 0;
        double actualSum = 0;
        double expectedSquareSum = 0;
        double productSum = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            var expected = template.Pixels[index];
            var x = index % FeatureSize;
            var y = index / FeatureSize;
            if (IsStatusOverlayPixel(x, y) || expected.Alpha < 160)
                continue;

            var expectedValue = Channel(expected, channel);
            var actualValue = Channel(actual[index], channel);
            count++;
            expectedSum += expectedValue;
            actualSum += actualValue;
            expectedSquareSum += expectedValue * expectedValue;
            productSum += expectedValue * actualValue;
        }
        if (count <= 0)
            return new LinearFit(1, 0);

        var denominator = (count * expectedSquareSum) - (expectedSum * expectedSum);
        var scale = Math.Abs(denominator) < 0.001
            ? 1
            : ((count * productSum) - (expectedSum * actualSum)) / denominator;
        var offset = (actualSum - (scale * expectedSum)) / count;
        return new LinearFit(Math.Clamp(scale, -1, 2), Math.Clamp(offset, -255, 255));
    }

    private static PlaneFit FitBackgroundPlane(
        CoinBaselineTemplate template,
        IReadOnlyList<SKColor> actual,
        int channel)
    {
        double xx = 0;
        double xy = 0;
        double xSum = 0;
        double yy = 0;
        double ySum = 0;
        double count = 0;
        double xv = 0;
        double yv = 0;
        double valueSum = 0;
        for (var index = 0; index < template.Pixels.Count; index++)
        {
            var expected = template.Pixels[index];
            var x = index % FeatureSize;
            var y = index / FeatureSize;
            if (IsStatusOverlayPixel(x, y) || expected.Alpha >= 32)
                continue;

            var value = Channel(actual[index], channel);
            xx += x * x;
            xy += x * y;
            xSum += x;
            yy += y * y;
            ySum += y;
            count++;
            xv += x * value;
            yv += y * value;
            valueSum += value;
        }
        if (count < 3)
            return new PlaneFit(0, 0, count <= 0 ? 0 : valueSum / count);

        var matrix = new[,]
        {
            { xx, xy, xSum, xv },
            { xy, yy, ySum, yv },
            { xSum, ySum, count, valueSum },
        };
        for (var column = 0; column < 3; column++)
        {
            var pivot = Enumerable.Range(column, 3 - column)
                .OrderByDescending(row => Math.Abs(matrix[row, column]))
                .First();
            if (Math.Abs(matrix[pivot, column]) < 0.0001)
                return new PlaneFit(0, 0, valueSum / count);
            if (pivot != column)
            {
                for (var item = column; item < 4; item++)
                    (matrix[column, item], matrix[pivot, item]) = (matrix[pivot, item], matrix[column, item]);
            }
            var divisor = matrix[column, column];
            for (var item = column; item < 4; item++)
                matrix[column, item] /= divisor;
            for (var row = 0; row < 3; row++)
            {
                if (row == column)
                    continue;
                var factor = matrix[row, column];
                for (var item = column; item < 4; item++)
                    matrix[row, item] -= factor * matrix[column, item];
            }
        }
        return new PlaneFit(matrix[0, 3], matrix[1, 3], matrix[2, 3]);
    }

    private static double Channel(BaselinePixel pixel, int channel) => channel switch
    {
        0 => pixel.Red,
        1 => pixel.Green,
        _ => pixel.Blue,
    };

    private static double Channel(SKColor pixel, int channel) => channel switch
    {
        0 => pixel.Red,
        1 => pixel.Green,
        _ => pixel.Blue,
    };

    private static IReadOnlyList<StatusTemplate> BuildStatusTemplates(
        IEnumerable<SukiSpecialEffectOption> options)
    {
        var templates = new List<StatusTemplate>();
        foreach (var option in options.Where(option => !string.IsNullOrWhiteSpace(option.ImagePath)))
        {
            using var source = SKBitmap.Decode(option.ImagePath);
            if (source is null || source.Width <= 0 || source.Height <= 0)
                continue;

            foreach (var width in StatusTemplateWidths)
            {
                var height = Math.Max(1, (int)Math.Round(width * (source.Height / (double)source.Width)));
                using var resized = Resize(source, width, height);
                var pixels = new List<StatusPixel>();
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        var color = resized.GetPixel(x, y);
                        if (color.Alpha < 176)
                            continue;

                        var alpha = color.Alpha / 255d;
                        pixels.Add(new StatusPixel(x, y, color.Red, color.Green, color.Blue, alpha * alpha));
                    }
                }
                if (pixels.Count >= 24)
                    templates.Add(new StatusTemplate(option, width, height, pixels));
            }
        }
        return templates;
    }

    private static IReadOnlyDictionary<string, CoinBaselineTemplate> BuildCoinTemplates(
        IEnumerable<SukiSpecialEffectOption> options)
    {
        var templates = new Dictionary<string, CoinBaselineTemplate>(StringComparer.Ordinal);
        foreach (var option in options.Where(option => !string.IsNullOrWhiteSpace(option.ImagePath)))
        {
            using var source = SKBitmap.Decode(option.ImagePath);
            if (source is null || source.Width <= 0 || source.Height <= 0)
                continue;

            using var resized = Resize(source, FeatureSize, FeatureSize);
            var pixels = new List<BaselinePixel>(FeatureSize * FeatureSize);
            for (var y = 0; y < FeatureSize; y++)
            {
                for (var x = 0; x < FeatureSize; x++)
                {
                    var color = resized.GetPixel(x, y);
                    pixels.Add(new BaselinePixel(color.Red, color.Green, color.Blue, color.Alpha));
                }
            }
            templates[option.Id] = new CoinBaselineTemplate(pixels);
        }
        return templates;
    }

    private static double ScreenTextCenterX(MaaRoi box) =>
        CoinListRoiX + ((box.X + (box.Width / 2d)) / CoinListOcrScale);

    private static double ScreenTextCenterY(MaaRoi box) =>
        CoinListRoiY + ((box.Y + (box.Height / 2d)) / CoinListOcrScale);

    private static MaaRoi CoinRoi(double centerX, double centerY) =>
        new(
            Math.Clamp((int)Math.Round(centerX) - 53, 0, BaseWidth - 106),
            Math.Clamp((int)Math.Round(centerY) - 53, 0, BaseHeight - 106),
            106,
            106);

    private static bool IsInside(SKBitmap frame, MaaRoi roi) =>
        roi.X >= 0
        && roi.Y >= 0
        && roi.X + roi.Width <= frame.Width
        && roi.Y + roi.Height <= frame.Height;

    private static SKBitmap NormalizeFrame(SKBitmap source)
    {
        if (source.Width == BaseWidth && source.Height == BaseHeight)
            return source.Copy();
        return Resize(source, BaseWidth, BaseHeight);
    }

    private static SKBitmap Resize(SKBitmap source, int width, int height)
    {
        return source.Resize(
                new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul),
                new SKSamplingOptions(SKCubicResampler.Mitchell))
            ?? new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
    }

    private sealed record StatusTemplate(
        SukiSpecialEffectOption Option,
        int Width,
        int Height,
        IReadOnlyList<StatusPixel> Pixels);

    private sealed record StatusPixel(
        int X,
        int Y,
        byte Red,
        byte Green,
        byte Blue,
        double Weight);

    private sealed record StatusMatch(
        StatusTemplate Template,
        double Score,
        double RunnerUpScore,
        MaaRoi Roi);

    private sealed record CoinBaselineTemplate(IReadOnlyList<BaselinePixel> Pixels);

    private sealed record BaselinePixel(byte Red, byte Green, byte Blue, byte Alpha);

    private sealed record LinearFit(double Scale, double Offset)
    {
        public double Predict(double value) => Math.Clamp((Scale * value) + Offset, 0, 255);
    }

    private sealed record PlaneFit(double X, double Y, double Offset)
    {
        public double Predict(double x, double y) => Math.Clamp((X * x) + (Y * y) + Offset, 0, 255);
    }
}
