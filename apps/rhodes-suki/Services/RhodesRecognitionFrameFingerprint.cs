using System.Numerics;
using SkiaSharp;

namespace RhodesSuki.Services;

public static class RhodesRecognitionFrameFingerprint
{
    public static ulong Compute(byte[] encodedImage, RhodesRecognitionSwipeArea area)
    {
        using var source = SKBitmap.Decode(encodedImage)
            ?? throw new InvalidOperationException("認識Frame画像をデコードできません。");
        var scaleX = source.Width / 1280d;
        var scaleY = source.Height / 720d;
        var left = Math.Clamp((int)Math.Round(area.X * scaleX), 0, source.Width - 1);
        var top = Math.Clamp((int)Math.Round(area.Y * scaleY), 0, source.Height - 1);
        var width = Math.Clamp((int)Math.Round(area.Width * scaleX), 1, source.Width - left);
        var height = Math.Clamp((int)Math.Round(area.Height * scaleY), 1, source.Height - top);

        using var cropped = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(cropped))
        {
            canvas.DrawBitmap(
                source,
                new SKRectI(left, top, left + width, top + height),
                new SKRect(0, 0, width, height));
        }

        using var reduced = cropped.Resize(new SKImageInfo(8, 8), SKSamplingOptions.Default)
            ?? throw new InvalidOperationException("認識Frame画像を縮小できません。");
        Span<int> luminance = stackalloc int[64];
        var total = 0;
        for (var y = 0; y < 8; y++)
        {
            for (var x = 0; x < 8; x++)
            {
                var color = reduced.GetPixel(x, y);
                var value = (color.Red * 299 + color.Green * 587 + color.Blue * 114) / 1000;
                luminance[y * 8 + x] = value;
                total += value;
            }
        }

        var average = total / 64;
        ulong fingerprint = 0;
        for (var index = 0; index < luminance.Length; index++)
        {
            if (luminance[index] >= average)
                fingerprint |= 1UL << index;
        }
        return fingerprint;
    }

    public static int Distance(ulong left, ulong right) => BitOperations.PopCount(left ^ right);
}
