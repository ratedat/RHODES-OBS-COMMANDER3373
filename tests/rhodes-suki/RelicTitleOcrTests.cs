using System.Text.Json;
using RhodesSuki.Models;
using RhodesSuki.Services;
using SkiaSharp;

namespace RhodesSuki.Tests;

public static class RelicTitleOcrTests
{
    public static void SeparatesColoredTitlesFromBackgroundText()
    {
        using var bitmap = new SKBitmap(1280, 720);
        bitmap.Erase(new SKColor(110, 110, 110));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.Cyan })
            canvas.DrawRect(810, 204, 180, 10, paint);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        var frameImage = MaaOwnedImage.FromEncodedCopy(png.ToArray());
        var frame = Ocr("RhodesOcrRegion_relic_list_text", "語られざる魔王の断片補足表示");
        var requests = RhodesMaaRelicTitleOcrExpander.BuildRequests(frame, frameImage, "is5_sarkaz");
        Check(requests.Count == 1, "only the unresolved colored title is retried");
        var request = requests.Single();
        Check(request.X == 795 && request.Y == 195 && request.Width == 270 && request.Height == 34,
            "retry crop follows the original OCR row");
        var prepared = RhodesMaaRecognitionImagePreprocessor.Prepare(frameImage, "OCR", request.PayloadJson, request.Scale, request.Entry);
        using var filtered = SKBitmap.Decode(prepared.EncodedImage);
        Check(filtered.GetPixel(60, 40).Red == 255 && filtered.GetPixel(0, 0).Red == 0,
            "cyan text becomes white while gray background is suppressed");

        var refined = RhodesMaaRelicTitleOcrExpander.RefineAsync(frame, frameImage, "is5_sarkaz",
            (retry, _) => Task.FromResult(Ocr(retry.Entry, "語られざる魔王の断片"))).GetAwaiter().GetResult();
        var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults("relicsFull",
            new[] { refined.Frame }.Concat(refined.Attempts), "is5_sarkaz");
        Check(candidates.Single().RelicId == "is5_sarkaz_relic_275", "clean title is recovered without an extra candidate");
        Check(candidates.Single().RawText == "語られざる魔王の断片補足表示", "candidate retains the original noisy OCR text");
        using var detail = JsonDocument.Parse(refined.Frame.RecognitionDetailJson);
        Check(detail.RootElement.GetProperty("filtered")[0].GetProperty("box")[0].GetInt32() == 800,
            "crop-local coordinates do not replace the original row");

        var rejected = RhodesMaaRelicTitleOcrExpander.RefineAsync(frame, frameImage, "is5_sarkaz",
            (retry, _) => Task.FromResult(Ocr(retry.Entry, "長居の手"))).GetAwaiter().GetResult();
        Check(rejected.Frame == frame, "a different formal relic cannot replace the expected title");
        Check(RhodesMaaRelicTitleOcrExpander.BuildRequests(frame, frameImage, "is4_sami").Count == 0, "campaign boundary");
        Check(RhodesMaaRelicTitleOcrExpander.BuildRequests(
            Ocr("RhodesOcrRegion_relic_list_text", "語らればる魔王の断片補足表示"), frameImage, "is5_sarkaz").Count == 1,
            "one title character error can schedule a retry without directly accepting the name");
        bitmap.Erase(SKColors.Gray);
        using var grayImage = SKImage.FromBitmap(bitmap);
        using var grayPng = grayImage.Encode(SKEncodedImageFormat.Png, 100);
        Check(RhodesMaaRelicTitleOcrExpander.BuildRequests(frame, MaaOwnedImage.FromEncodedCopy(grayPng.ToArray()), "is5_sarkaz").Count == 0,
            "plain description text does not trigger a title retry");
    }

    private static MaaTaskRunResult Ocr(string entry, string text) => new(entry, "Succeeded", true, "synthetic OCR",
        JsonSerializer.Serialize(new { filtered = new[] { new { text, score = 0.99, box = new[] { 800, 200, 260, 24 } } } }), "OCR", true);
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
