using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Services;

/// <summary>Separates colored list titles and adjacent usage labels from effect prose.</summary>
public static class RhodesMaaRelicTitleImage
{
    public const string ListEntry = "RhodesOcrRegion_relic_list_text";

    public static IReadOnlyList<SKRectI> FindTitleRegions(MaaOwnedImage image)
    {
        if (image.Length == 0) return [];
        using var source = image.CreateBitmap();
        if (source.Width != 1280 || source.Height != 720) return [];
        var regions = new List<SKRectI>();
        foreach (var (left, right) in new[] { (172, 446), (562, 836), (952, 1270) })
        foreach (var (top, bottom) in FindTitleBands(source, left))
        {
            var first = right; var last = left;
            for (var y = top; y < bottom; y++)
            for (var x = left; x < right; x++)
                if (RhodesMaaRelicTitleOcrExpander.IsTitleForeground(source.GetPixel(x, y)))
                { first = Math.Min(first, x); last = Math.Max(last, x); }
            if (last >= first)
                regions.Add(new SKRectI(Math.Max(left, first - 2), top, Math.Min(right, last + 3), bottom));
        }
        return regions;
    }

    public static MaaOwnedImage Prepare(MaaOwnedImage image, string entry, string campaignId)
    {
        if (entry != ListEntry || campaignId != "is5_sarkaz" || image.Length == 0) return image;
        using var source = image.CreateBitmap();
        if (source.Width != 1280 || source.Height != 720) return image;
        using var titles = new SKBitmap(source.Width, source.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
        titles.Erase(SKColors.Black);
        foreach (var (left, right) in new[] { (172, 446), (562, 836), (952, 1270) })
        {
            foreach (var (top, bottom) in FindTitleBands(source, left))
            {
                var lastTitleX = left;
                for (var y = top; y < bottom; y++)
                for (var x = left; x < right; x++)
                {
                    var color = source.GetPixel(x, y);
                    if (color.Green < 35 || color.Blue < 35 || color.Green - color.Red < 8
                        || color.Blue - color.Red < 8 || Math.Abs(color.Green - color.Blue) > 80) continue;
                    titles.SetPixel(x, y, color);
                    lastTitleX = Math.Max(lastTitleX, x);
                }
                // Usage labels sit beside the title. Retain their original position
                // so the existing proximity check cannot borrow prose from below.
                for (var y = top; y < bottom; y++)
                for (var x = lastTitleX + 4; x < Math.Min(right, lastTitleX + 100); x++)
                {
                    var color = source.GetPixel(x, y);
                    var low = Math.Min(color.Red, Math.Min(color.Green, color.Blue));
                    var high = Math.Max(color.Red, Math.Max(color.Green, color.Blue));
                    if (low >= 140 && high - low <= 40) titles.SetPixel(x, y, SKColors.White);
                }
            }
        }
        return MaaOwnedImage.FromBitmap(titles);
    }

    private static IEnumerable<(int Top, int Bottom)> FindTitleBands(SKBitmap source, int left)
    {
        var rows = new List<int>();
        for (var y = 24; y < 624; y++)
        {
            var pixels = 0;
            for (var x = left; x < left + 64; x++)
                if (RhodesMaaRelicTitleOcrExpander.IsTitleForeground(source.GetPixel(x, y))) pixels++;
            if (pixels >= 5) rows.Add(y);
        }
        for (var i = 0; i < rows.Count;)
        {
            var first = rows[i];
            var last = first;
            while (++i < rows.Count && rows[i] - last <= 2) last = rows[i];
            if (last - first is >= 7 and <= 26)
                yield return (Math.Max(24, first - 4), Math.Min(624, last + 5));
        }
    }
}
