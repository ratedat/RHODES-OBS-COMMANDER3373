using System.Text.Json;
using RhodesSuki.Models;
using RhodesSuki.Services;
using SkiaSharp;

public static class RecognitionNativeImageTests
{
    public static void RawAndPngProduceSameOcr()
    {
        using var session = new RhodesMaaSession();
        var ready = session.InitializeOfflineAsync(RhodesMaaSession.DefaultAdbOptions()).GetAwaiter().GetResult();
        Check(ready.IsReady, "offline recognition is available");
        using var bitmap = new SKBitmap(1280, 720, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var font = new SKFont(SKTypeface.Default, 90);
            using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
            canvas.DrawText("3373", 100, 220, SKTextAlign.Left, font, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        AssertParity(session, data.ToArray(), new MaaRoi(50, 100, 400, 180), requireHit: true);

        var corpus = Environment.GetEnvironmentVariable("RHODES_SUKI_NATIVE_PARITY_MANIFEST");
        if (string.IsNullOrWhiteSpace(corpus)) return;
        using var manifest = JsonDocument.Parse(File.ReadAllText(corpus));
        foreach (var row in manifest.RootElement.GetProperty("records").EnumerateArray()
                     .GroupBy(row => row.GetProperty("profile").GetString()).Select(group => group.First()))
        {
            var path = row.GetProperty("imagePath").GetString()!;
            AssertParity(session, File.ReadAllBytes(path), new MaaRoi(0, 0, 1280, 720), requireHit: false);
            Console.WriteLine($"native-image-parity historical profile={row.GetProperty("profile").GetString()} passed");
        }
    }

    private static void AssertParity(RhodesMaaSession session, byte[] png, MaaRoi roi, bool requireHit)
    {
        using var bitmap = SKBitmap.Decode(png);
        var pixels = new byte[checked(bitmap.Width * bitmap.Height * 3)];
        for (var y = 0; y < bitmap.Height; y++)
            for (var x = 0; x < bitmap.Width; x++)
            {
                var color = bitmap.GetPixel(x, y);
                var index = (y * bitmap.Width + x) * 3;
                pixels[index] = color.Blue;
                pixels[index + 1] = color.Green;
                pixels[index + 2] = color.Red;
            }
        var raw = MaaOwnedImage.FromOwnedRaw(pixels, bitmap.Width, bitmap.Height, 3, 16);
        var plan = RhodesRecognitionLab.BuildPlan(new RhodesRecognitionLabRequest("ocr", "fixture.png", roi,
            Threshold: 0.3, OnlyRecognition: false));
        foreach (var scale in new[] { 1, 2 })
        {
            var encodedResult = session.RunResourceRecognitionAsync("image-parity", plan.PayloadJson, png, scaleOverride: scale).GetAwaiter().GetResult();
            var rawResult = session.RunResourceRecognitionAsync("image-parity", plan.PayloadJson, raw, scaleOverride: scale).GetAwaiter().GetResult();
            Check(encodedResult.Succeeded && rawResult.Succeeded, "both native image paths execute");
            if (requireHit) Check(encodedResult.Hit && rawResult.Hit, "fixture text is actually recognized");
            Check(encodedResult.Hit == rawResult.Hit && encodedResult.RecognitionDetailJson == rawResult.RecognitionDetailJson,
                $"native OCR output remains identical at scale {scale}");
        }
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
