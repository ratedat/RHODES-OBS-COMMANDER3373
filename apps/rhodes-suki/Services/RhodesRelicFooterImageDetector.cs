using SkiaSharp;

namespace RhodesSuki.Services;

public sealed record RhodesRelicFooterImageInspection(
    RhodesRelicFooterAvailability Availability,
    int BrightPixels,
    int ChromaPixels,
    int EdgePixels,
    double MeanLuminance,
    string Detail);

public static class RhodesRelicFooterImageDetector
{
    private const int BaseWidth = 1280;
    private const int BaseHeight = 720;
    private const int ThumbnailStripX = 220;
    private const int ThumbnailStripY = 642;
    private const int ThumbnailStripWidth = 150;
    private const int ThumbnailStripHeight = 70;

    public static RhodesRelicFooterImageInspection Inspect(byte[] encodedImage)
    {
        if (encodedImage.Length == 0)
            return Unknown("image is empty");

        using var bitmap = TryDecode(encodedImage);
        if (bitmap is null || bitmap.Width <= 0 || bitmap.Height <= 0)
            return Unknown("image decode failed");

        var left = Scale(ThumbnailStripX, bitmap.Width, BaseWidth);
        var top = Scale(ThumbnailStripY, bitmap.Height, BaseHeight);
        var width = Math.Max(1, Scale(ThumbnailStripWidth, bitmap.Width, BaseWidth));
        var height = Math.Max(1, Scale(ThumbnailStripHeight, bitmap.Height, BaseHeight));
        var right = Math.Min(bitmap.Width, left + width);
        var bottom = Math.Min(bitmap.Height, top + height);
        if (left < 0 || top < 0 || right <= left || bottom <= top)
            return Unknown("thumbnail strip is outside the frame");

        var sampled = 0;
        var brightPixels = 0;
        var chromaPixels = 0;
        var edgePixels = 0;
        var luminanceSum = 0d;
        for (var y = top; y < bottom; y++)
        {
            var previousLuminance = 0d;
            for (var x = left; x < right; x++)
            {
                var color = bitmap.GetPixel(x, y);
                var luminance = Luminance(color);
                sampled++;
                luminanceSum += luminance;
                if (luminance >= 100)
                    brightPixels++;
                if (Math.Max(color.Red, Math.Max(color.Green, color.Blue))
                    - Math.Min(color.Red, Math.Min(color.Green, color.Blue)) >= 20)
                {
                    chromaPixels++;
                }
                if (x > left && Math.Abs(luminance - previousLuminance) >= 35)
                    edgePixels++;
                previousLuminance = luminance;
            }
        }

        var brightRatio = Ratio(brightPixels, sampled);
        var chromaRatio = Ratio(chromaPixels, sampled);
        var edgeRatio = Ratio(edgePixels, sampled);
        var meanLuminance = sampled == 0 ? 0 : luminanceSum / sampled;
        var hasThumbnail = brightRatio >= 0.02
            || chromaRatio >= 0.004
            || edgeRatio >= 0.01;
        var isBlank = brightRatio <= 0.003
            && chromaRatio <= 0.002
            && edgeRatio <= 0.003
            && meanLuminance <= 32;
        var availability = hasThumbnail
            ? RhodesRelicFooterAvailability.HasOwnedRelics
            : isBlank
                ? RhodesRelicFooterAvailability.Empty
                : RhodesRelicFooterAvailability.Unknown;
        return new RhodesRelicFooterImageInspection(
            availability,
            brightPixels,
            chromaPixels,
            edgePixels,
            Math.Round(meanLuminance, 2),
            $"roi={left},{top},{right - left},{bottom - top}; bright={brightRatio:P2}; chroma={chromaRatio:P2}; edge={edgeRatio:P2}; mean={meanLuminance:F2}");
    }

    private static RhodesRelicFooterImageInspection Unknown(string detail) =>
        new(RhodesRelicFooterAvailability.Unknown, 0, 0, 0, 0, detail);

    private static SKBitmap? TryDecode(byte[] encodedImage)
    {
        try
        {
            return SKBitmap.Decode(encodedImage);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }

    private static int Scale(int value, int actual, int basis) =>
        (int)Math.Round(value * actual / (double)basis, MidpointRounding.AwayFromZero);

    private static double Ratio(int value, int total) => total == 0 ? 0 : value / (double)total;

    private static double Luminance(SKColor color) =>
        (0.2126 * color.Red) + (0.7152 * color.Green) + (0.0722 * color.Blue);
}
