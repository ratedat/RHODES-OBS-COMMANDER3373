using RhodesSuki.Services;
using RhodesSuki.Models;
using SkiaSharp;

namespace RhodesSuki.Tests;

public static class ThoughtTitleImageTests
{
    public static void ExcludesDescriptionsBeforeOcr()
    {
        using var source = new SKBitmap(1280, 720);
        source.Erase(SKColors.Black);
        using (var canvas = new SKCanvas(source))
        using (var paint = new SKPaint { Color = SKColors.White })
        {
            foreach (var (x, y) in new[] { (395, 180), (827, 180), (395, 420) })
            {
                using var star = new SKPath();
                star.MoveTo(x, y - 9);
                star.LineTo(x + 3, y - 3);
                star.LineTo(x + 9, y);
                star.LineTo(x + 3, y + 3);
                star.LineTo(x, y + 9);
                star.LineTo(x - 3, y + 3);
                star.LineTo(x - 9, y);
                star.LineTo(x - 3, y - 3);
                star.Close();
                canvas.DrawPath(star, paint);
            }
            // Artificial title and prose blocks use identical colors. Text content
            // cannot distinguish a title from a description mentioning another title.
            foreach (var x in new[] { 480, 912 })
            {
                canvas.DrawRect(x, 180, 100, 18, paint);
                canvas.DrawRect(x, 210, 100, 18, paint);
            }
            canvas.DrawRect(480, 420, 100, 18, paint);
            canvas.DrawRect(480, 450, 100, 18, paint);
            canvas.DrawRect(430, 350, 300, 20, paint); // section header
            canvas.DrawRect(385, 270, 24, 24, paint); // solid icon detail, not a star
            canvas.DrawRect(480, 280, 100, 18, paint);
        }
        var input = MaaOwnedImage.FromBitmap(source);
        var prepared = RhodesMaaRecognitionImagePreprocessor.Prepare(input, "OCR",
            "{\"roi\":[355,84,835,540]}", 1, "RhodesOcrRegion_is5_thought_list_text");
        using var actual = prepared.Image.CreateBitmap();
        Check(actual.Width == 1280 && actual.Height == 720, "original frame coordinates must survive");
        foreach (var point in new[] { (490, 190), (922, 190), (490, 430) })
            Check(actual.GetPixel(point.Item1, point.Item2) == SKColors.White, "card title remains readable");
        foreach (var point in new[] { (490, 220), (922, 220), (490, 460), (490, 360), (490, 290), (395, 180) })
            Check(actual.GetPixel(point.Item1, point.Item2) == SKColors.Black, "prose, headers and icons never reach OCR");
        using var unchanged = input.CreateBitmap();
        Check(unchanged.GetPixel(490, 220) == SKColors.White, "source frame remains available for numeric OCR");
        var scaled = RhodesMaaRecognitionImagePreprocessor.Prepare(input, "OCR",
            "{\"roi\":[470,170,120,70]}", 2, "RhodesOcrRegion_is5_thought_list_text");
        using var resized = scaled.Image.CreateBitmap();
        Check(resized.Width == 240 && resized.Height == 140, "explicit crop scaling remains supported");
        Check(resized.GetPixel(40, 40) == SKColors.White && resized.GetPixel(40, 100) == SKColors.Black,
            "scaling does not reintroduce descriptions");
    }

    public static void UnlocatedCardsNeverFallBackToProse()
    {
        using var source = new SKBitmap(1280, 720);
        source.Erase(new SKColor(80, 80, 80));
        using (var canvas = new SKCanvas(source))
        using (var paint = new SKPaint { Color = SKColors.White })
            canvas.DrawRect(480, 220, 120, 18, paint);
        var input = MaaOwnedImage.FromBitmap(source);
        var prepared = RhodesMaaRecognitionImagePreprocessor.Prepare(input, "OCR",
            "{\"roi\":[355,84,835,540]}", 1, "RhodesOcrRegion_is5_thought_list_text");
        using var actual = prepared.Image.CreateBitmap();
        Check(actual.GetPixel(490, 225) == SKColors.Black, "unlocated cards cannot expose prose");
        var unrelated = RhodesMaaRecognitionImagePreprocessor.Prepare(input, "OCR",
            "{\"roi\":[355,84,835,540]}", 1, "RhodesOcrRegion_relic_list_text");
        Check(ReferenceEquals(unrelated.Image, input), "other recognition inputs are unchanged");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
