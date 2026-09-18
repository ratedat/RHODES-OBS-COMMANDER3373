using RhodesSuki.Models;
using RhodesSuki.Services;
using SkiaSharp;

namespace RhodesSuki.Tests;

public static class RelicTitleImageTests
{
    public static void KeepsNamesAndUsageWithoutProse()
    {
        using var bitmap = new SKBitmap(1280, 720);
        bitmap.Erase(SKColors.Black);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = new SKColor(60, 200, 205) })
        {
            foreach (var (x, y) in new[] { (176, 48), (566, 175), (956, 302) })
            {
                paint.Color = new SKColor(60, 200, 205);
                canvas.DrawRect(x, y, 100, 18, paint);
                paint.Color = SKColors.White;
                canvas.DrawRect(x + 115, y, 45, 18, paint);
                canvas.DrawRect(x, y + 29, 220, 18, paint);
                canvas.DrawRect(x - 55, y + 55, 25, 18, paint);
            }
        }
        var source = MaaOwnedImage.FromBitmap(bitmap);
        var prepared = RhodesMaaRelicTitleImage.Prepare(source, RhodesMaaRelicTitleImage.ListEntry, "is5_sarkaz");
        using var actual = prepared.CreateBitmap();
        Check(actual.Width == 1280 && actual.Height == 720, "title boxes must keep frame coordinates");
        foreach (var (x, y) in new[] { (176, 48), (566, 175), (956, 302) })
        {
            Check(actual.GetPixel(x + 10, y + 8) == new SKColor(60, 200, 205), "colored title remains");
            Check(actual.GetPixel(x + 120, y + 8) == SKColors.White, "adjacent usage label remains");
            Check(actual.GetPixel(x + 10, y + 35) == SKColors.Black, "effect prose is excluded");
            Check(actual.GetPixel(x - 50, y + 60) == SKColors.Black, "stack digits use their separate input");
        }
        using var original = source.CreateBitmap();
        Check(original.GetPixel(186, 83) == SKColors.White, "numeric and retry OCR retain the untouched source");
    }

    public static void LimitsProcessingToTheSupportedList()
    {
        using var bitmap = new SKBitmap(1280, 720);
        bitmap.Erase(SKColors.White);
        var source = MaaOwnedImage.FromBitmap(bitmap);
        Check(ReferenceEquals(source, RhodesMaaRelicTitleImage.Prepare(source, "relic.stack.example", "is5_sarkaz")), "numeric OCR stays unchanged");
        Check(ReferenceEquals(source, RhodesMaaRelicTitleImage.Prepare(source, RhodesMaaRelicTitleImage.ListEntry, "is4_sami")), "other campaign layouts stay unchanged");
        using var masked = RhodesMaaRelicTitleImage.Prepare(source, RhodesMaaRelicTitleImage.ListEntry, "is5_sarkaz").CreateBitmap();
        Check(masked.GetPixel(200, 100) == SKColors.Black, "white prose without a colored title is excluded");
    }

    public static void RetriesUnresolvedTitleBands()
    {
        using var bitmap = new SKBitmap(1280, 720);
        bitmap.Erase(SKColors.Black);
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { Color = SKColors.Cyan })
            canvas.DrawRect(176, 180, 140, 18, paint);
        var image = MaaOwnedImage.FromBitmap(bitmap);
        var frame = new MaaTaskRunResult(RhodesMaaRelicTitleImage.ListEntry, "Succeeded", true, "",
            RecognitionDetailJson: """
            {"filtered":[
              {"text":"錆刃・I","score":0.81,"box":[176,179,63,20]},
              {"text":"隠遁超然","score":0.99,"box":[231,177,85,22]},
              {"text":"使用済み","score":0.99,"box":[330,180,60,20]}
            ]}
            """, Hit: true);
        frame = RhodesMaaRelicTitleOcrExpander.MarkTitleOnly(frame);
        MaaTaskRunResult Retry(MaaDynamicOcrRequest request, string text, double score) => new(
            request.Entry, "Succeeded", true, "", RecognitionDetailJson: System.Text.Json.JsonSerializer.Serialize(
                new { filtered = new[] { new { text, score, box = new[] { 0, 0, 500, 90 } } } }), Hit: true);
        var refinement = RhodesMaaRelicTitleOcrExpander.RefineAsync(frame, image, "is5_sarkaz",
            (request, _) => Task.FromResult(Retry(request, "錆刃・隠遁超然", 0.99))).GetAwaiter().GetResult();
        Check(refinement.Attempts.Count == 1, "split fragments cause one full title retry");
        using var document = System.Text.Json.JsonDocument.Parse(refinement.Frame.RecognitionDetailJson!);
        var rows = document.RootElement.GetProperty("filtered").EnumerateArray().ToArray();
        Check(rows.Length == 2 && rows.Any(row => row.GetProperty("text").GetString() == "使用済み"),
            "fragments are replaced while the adjacent usage label survives");
        var named = rows.Single(row => row.GetProperty("text").GetString() == "錆刃・隠遁超然");
        Check(named.GetProperty("box")[0].GetInt32() == 174 && named.GetProperty("box")[2].GetInt32() == 144,
            "retry uses source title bounds rather than scaled OCR coordinates");
        var repeated = RhodesMaaRelicTitleOcrExpander.RefineAsync(refinement.Frame, image, "is5_sarkaz",
            (_, _) => throw new InvalidOperationException("resolved title was unnecessarily retried")).GetAwaiter().GetResult();
        Check(repeated.Attempts.Count == 0, "resolved titles are reused even when their original OCR was split");
        foreach (var answer in new[] { ("未知の名前", 0.99), ("錆刃・隠遁超然", 0.5) })
        {
            var rejected = RhodesMaaRelicTitleOcrExpander.RefineAsync(frame, image, "is5_sarkaz",
                (request, _) => Task.FromResult(Retry(request, answer.Item1, answer.Item2))).GetAwaiter().GetResult();
            Check(rejected.Frame == frame, "unknown or low confidence retry remains unresolved");
        }
        var calls = 0;
        var corroborated = RhodesMaaRelicTitleOcrExpander.RefineAsync(frame, image, "is5_sarkaz",
            (request, _) => Task.FromResult(Retry(request, "錆刃・隠遁超然", ++calls == 1 ? 0.87 : 0.89))).GetAwaiter().GetResult();
        Check(calls == 2 && corroborated.Frame != frame, "borderline exact names require agreement at two scales");
        using var corroboratedJson = System.Text.Json.JsonDocument.Parse(corroborated.Frame.RecognitionDetailJson!);
        Check(corroboratedJson.RootElement.GetProperty("filtered").EnumerateArray()
            .Single(row => row.GetProperty("text").GetString() == "錆刃・隠遁超然").GetProperty("score").GetDouble() == 0.87,
            "corroboration retains the lower observed confidence");
        calls = 0;
        var disagreement = RhodesMaaRelicTitleOcrExpander.RefineAsync(frame, image, "is5_sarkaz",
            (request, _) => Task.FromResult(Retry(request, ++calls == 1 ? "錆刃・隠遁超然" : "長居の手", 0.87))).GetAwaiter().GetResult();
        Check(calls == 2 && disagreement.Frame == frame, "disagreeing complete names remain unresolved");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
