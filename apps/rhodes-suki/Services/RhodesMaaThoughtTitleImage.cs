using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

/// <summary>Retains only title bands located from the leading four-point card marker.</summary>
public static class RhodesMaaThoughtTitleImage
{
    public const string ListEntry = "RhodesOcrRegion_is5_thought_list_text";

    public static MaaOwnedImage Prepare(MaaOwnedImage image)
    {
        using var source = image.CreateBitmap();
        using var titles = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        titles.Erase(SKColors.Black);
        // Recognition profiles and subsequent card/load tracking share this canvas.
        // Keep its coordinates, including when no title can be located.
        if (source.Width == 1280 && source.Height == 720)
        {
            using var canvas = new SKCanvas(titles);
            foreach (var (markerX, titleX, right) in new[] { (395, 474, 790), (827, 906, 1190) })
            {
                var centers = FindMarkerCenters(source, markerX);
                foreach (var center in centers)
                {
                    var top = Math.Max(84, center - 5);
                    var bottom = Math.Min(624, center + 25);
                    var band = new SKRect(titleX, top, right, bottom);
                    canvas.DrawBitmap(source, band, band);
                }
            }
        }
        return MaaOwnedImage.FromBitmap(titles);
    }

    private static IReadOnlyList<int> FindMarkerCenters(SKBitmap source, int markerX)
    {
        var candidates = new List<int>();
        for (var y = 92; y <= 599; y++)
        {
            for (var x = markerX - 2; x <= markerX + 2; x++)
            {
                if (!IsMarker(source, x, y)) continue;
                candidates.Add(y);
                break;
            }
        }

        var centers = new List<int>();
        for (var index = 0; index < candidates.Count;)
        {
            var first = candidates[index];
            var last = first;
            while (++index < candidates.Count && candidates[index] - last <= 3)
                last = candidates[index];
            // A marker is compact; bright icon regions and solid section labels
            // must not establish an additional title band.
            if (last - first <= 10)
                centers.Add((first + last) / 2);
        }
        return centers.Where(center => !centers.Any(other => other != center
            && Math.Abs(other - center) < 64)).ToArray();
    }

    private static bool IsMarker(SKBitmap source, int x, int y)
    {
        if (!IsBright(source.GetPixel(x, y))
            || !IsBright(source.GetPixel(x - 5, y)) || !IsBright(source.GetPixel(x + 5, y))
            || !IsBright(source.GetPixel(x, y - 5)) || !IsBright(source.GetPixel(x, y + 5)))
            return false;
        var corners = 0;
        foreach (var dx in new[] { -5, 5 })
            foreach (var dy in new[] { -5, 5 })
                if (IsBright(source.GetPixel(x + dx, y + dy))) corners++;
        return corners <= 1;
    }

    private static bool IsBright(SKColor color) =>
        Math.Min(color.Red, Math.Min(color.Green, color.Blue)) >= 150
        && Math.Max(color.Red, Math.Max(color.Green, color.Blue))
            - Math.Min(color.Red, Math.Min(color.Green, color.Blue)) <= 35;
}
