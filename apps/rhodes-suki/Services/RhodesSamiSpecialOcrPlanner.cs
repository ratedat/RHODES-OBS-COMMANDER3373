using SkiaSharp;

namespace RhodesSuki.Services;

public static class RhodesSamiSpecialOcrPlanner
{
    private const int RecognitionScale = 3;

    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const double FarSightBrightRatio = 0.09;
    private const double RevelationBandColorRatio = 0.08;

    private static readonly (int X, int Y, int Width, int Height)[] RevelationTopRows =
    [
        (20, 142, 397, 126),
        (20, 276, 397, 126),
        (20, 411, 397, 126),
    ];

    private static readonly (int X, int Y, int Width, int Height)[] RevelationBottomRows =
    [
        (20, 0, 397, 130),
        (20, 200, 397, 128),
        (20, 335, 397, 128),
        (20, 470, 397, 132),
    ];

    private static readonly (int X, int Y, int Width, int Height)[] ParadigmRows =
    [
        (335, 95, 655, 95),
        (335, 190, 655, 95),
        (335, 285, 655, 95),
        (335, 380, 655, 95),
    ];

    public static IReadOnlyList<MaaDynamicOcrRequest> BuildRequests(
        string? profileId,
        byte[]? encodedImage = null)
    {
        return profileId switch
        {
            "is4RevelationFull" => BuildRevelationRows(encodedImage),
            "is4ParadigmLost" => BuildRows(
                "RhodesOcrRegion_is4.paradigm_lost_text.row",
                ParadigmRows),
            _ => [],
        };
    }

    private static IReadOnlyList<MaaDynamicOcrRequest> BuildRevelationRows(byte[]? encodedImage)
    {
        if (encodedImage is not { Length: > 0 })
        {
            return BuildRows(
                "RhodesOcrRegion_is4.revelation_list_text.row",
                RevelationTopRows);
        }

        using var bitmap = SKBitmap.Decode(encodedImage);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
        {
            return BuildRows(
                "RhodesOcrRegion_is4.revelation_list_text.row",
                RevelationTopRows);
        }

        var detectedRows = DetectRevelationRows(bitmap);
        if (detectedRows.Count > 0)
        {
            return detectedRows
                .Select((row, index) =>
                {
                    var farSightSuffix = IsFarSightPreview(bitmap, row) ? ".far_sight" : "";
                    return new MaaDynamicOcrRequest(
                        $"RhodesOcrRegion_is4.revelation_list_text.dynamic.row_{index + 1}{farSightSuffix}",
                        row.X,
                        row.Y,
                        row.Width,
                        row.Height,
                        RecognitionScale,
                        1.0,
                        OnlyRecognition: false);
                })
                .ToArray();
        }

        var upperVariation = GrayStandardDeviation(bitmap, 20, 75, 397, 65);
        var lowerVariation = GrayStandardDeviation(bitmap, 20, 135, 397, 65);
        var isBottomViewport = upperVariation > lowerVariation + 4.0;
        var rows = isBottomViewport ? RevelationBottomRows : RevelationTopRows;
        var viewport = isBottomViewport ? "bottom" : "top";

        return rows
            .Select((row, index) =>
            {
                var farSightSuffix = IsFarSightPreview(bitmap, row) ? ".far_sight" : "";
                return new MaaDynamicOcrRequest(
                    $"RhodesOcrRegion_is4.revelation_list_text.{viewport}.row_{index + 1}{farSightSuffix}",
                    row.X,
                    row.Y,
                    row.Width,
                    row.Height,
                    RecognitionScale,
                    1.0,
                    OnlyRecognition: false);
            })
            .ToArray();
    }

    private static IReadOnlyList<(int X, int Y, int Width, int Height)> DetectRevelationRows(
        SKBitmap bitmap)
    {
        const int scanStartX = 25;
        const int scanEndX = 135;
        const int scanEndY = 610;
        const int smoothingRadius = 3;
        const int mergeGap = 8;
        const int minimumBandHeight = 60;

        var colorRatios = new double[scanEndY];
        for (var baseY = 0; baseY < scanEndY; baseY++)
        {
            var y1 = Math.Clamp(ScaleY(bitmap, baseY), 0, bitmap.Height - 1);
            var y2 = Math.Clamp(ScaleY(bitmap, baseY + 1), y1 + 1, bitmap.Height);
            var colored = 0;
            var pixels = 0;

            for (var baseX = scanStartX; baseX < scanEndX; baseX += 2)
            {
                var x = Math.Clamp(ScaleX(bitmap, baseX), 0, bitmap.Width - 1);
                for (var y = y1; y < y2; y++)
                {
                    var color = bitmap.GetPixel(x, y);
                    var maximum = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
                    var minimum = Math.Min(color.Red, Math.Min(color.Green, color.Blue));
                    pixels++;
                    if (maximum >= 45 && maximum - minimum >= 18)
                        colored++;
                }
            }

            colorRatios[baseY] = pixels == 0 ? 0 : colored / (double)pixels;
        }

        var activeRows = new bool[colorRatios.Length];
        for (var y = 0; y < colorRatios.Length; y++)
        {
            var start = Math.Max(0, y - smoothingRadius);
            var end = Math.Min(colorRatios.Length - 1, y + smoothingRadius);
            var average = 0.0;
            for (var sample = start; sample <= end; sample++)
                average += colorRatios[sample];
            average /= end - start + 1;
            activeRows[y] = average >= RevelationBandColorRatio;
        }

        var bands = new List<(int Start, int End)>();
        for (var y = 0; y < activeRows.Length; y++)
        {
            if (!activeRows[y])
                continue;

            var start = y;
            while (y + 1 < activeRows.Length && activeRows[y + 1])
                y++;
            bands.Add((start, y));
        }

        var merged = new List<(int Start, int End)>();
        foreach (var band in bands)
        {
            if (merged.Count > 0 && band.Start - merged[^1].End - 1 <= mergeGap)
            {
                var previous = merged[^1];
                merged[^1] = (previous.Start, band.End);
            }
            else
            {
                merged.Add(band);
            }
        }

        return merged
            .Where(band => band.End - band.Start + 1 >= minimumBandHeight)
            .Select(band =>
            {
                var rowY = Math.Max(0, band.Start - 10);
                return (X: 20, Y: rowY, Width: 397, Height: Math.Min(132, BaseHeight - rowY));
            })
            .ToArray();
    }

    private static bool IsFarSightPreview(
        SKBitmap bitmap,
        (int X, int Y, int Width, int Height) row)
    {
        var x1 = ScaleX(bitmap, 390);
        var x2 = ScaleX(bitmap, 417);
        var y1 = ScaleY(bitmap, row.Y + 4);
        var y2 = ScaleY(bitmap, row.Y + 38);
        var bright = 0;
        var pixels = 0;

        for (var y = Math.Max(0, y1); y < Math.Min(bitmap.Height, y2); y++)
        {
            for (var x = Math.Max(0, x1); x < Math.Min(bitmap.Width, x2); x++)
            {
                var color = bitmap.GetPixel(x, y);
                pixels++;
                if (color.Red >= 210 && color.Green >= 210 && color.Blue >= 210)
                    bright++;
            }
        }

        return pixels > 0 && bright / (double)pixels >= FarSightBrightRatio;
    }

    private static double GrayStandardDeviation(
        SKBitmap bitmap,
        int x,
        int y,
        int width,
        int height)
    {
        var x1 = Math.Max(0, ScaleX(bitmap, x));
        var x2 = Math.Min(bitmap.Width, ScaleX(bitmap, x + width));
        var y1 = Math.Max(0, ScaleY(bitmap, y));
        var y2 = Math.Min(bitmap.Height, ScaleY(bitmap, y + height));
        var count = 0L;
        var sum = 0.0;
        var squared = 0.0;

        for (var py = y1; py < y2; py += Math.Max(1, (y2 - y1) / 80))
        {
            for (var px = x1; px < x2; px += Math.Max(1, (x2 - x1) / 160))
            {
                var color = bitmap.GetPixel(px, py);
                var gray = (color.Red * 0.299) + (color.Green * 0.587) + (color.Blue * 0.114);
                count++;
                sum += gray;
                squared += gray * gray;
            }
        }

        if (count == 0)
            return 0;

        var mean = sum / count;
        return Math.Sqrt(Math.Max(0, (squared / count) - (mean * mean)));
    }

    private static int ScaleX(SKBitmap bitmap, int value) =>
        (int)Math.Round(value * bitmap.Width / (double)BaseWidth);

    private static int ScaleY(SKBitmap bitmap, int value) =>
        (int)Math.Round(value * bitmap.Height / (double)BaseHeight);

    private static IReadOnlyList<MaaDynamicOcrRequest> BuildRows(
        string entryPrefix,
        IReadOnlyList<(int X, int Y, int Width, int Height)> rows)
    {
        return rows
            .Select((row, index) => new MaaDynamicOcrRequest(
                $"{entryPrefix}_{index + 1}",
                row.X,
                row.Y,
                row.Width,
                row.Height,
                RecognitionScale,
                1.0,
                OnlyRecognition: false))
            .ToArray();
    }
}
