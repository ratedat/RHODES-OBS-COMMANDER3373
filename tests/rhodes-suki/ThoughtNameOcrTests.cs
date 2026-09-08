using System.Text.Json;
using RhodesSuki.Models;
using RhodesSuki.Services;

namespace RhodesSuki.Tests;

public static class ThoughtNameOcrTests
{
    public static void RefinesNamesWithinTheirOriginalFrame()
    {
        var source = Frame(
            ("枯れ木と若枝", 500, 140),
            ("走る都市", 500, 260),
            ("功名の石枝", 900, 140),
            ("土を被った食具", 900, 260),
            ("功名の石枝", 900, 380),
            ("壁の中の純金", 500, 380));
        var requests = RhodesMaaThoughtNameOcrExpander.BuildRequests(source);
        Check(requests.Count == 3, "only unresolved, aligned title rows are retried");
        Check(requests[0].X == 895 && requests[0].Y == 135
            && requests[0].Width == 150 && requests[0].Height == 30
            && requests[0].Scale == 4 && requests[0].OnlyRecognition, "padded title crop");
        var index = 0;
        var refinement = RhodesMaaThoughtNameOcrExpander.RefineAsync(source, (request, _) =>
        {
            var name = index++ == 1 ? "土を被った食器" : "功名の石柱";
            // A scaled crop has crop-local coordinates, which must not replace frame coordinates.
            return Task.FromResult(RowResult(request.Entry, name, 0.98, 0, 0));
        }).GetAwaiter().GetResult();

        Check(index == 3 && refinement.Attempts.Count == 3, "one retry per unresolved title");
        var candidates = RhodesMaaLocalCandidateConverter.FromTaskResults(
            "is5ThoughtFull", new[] { refinement.Frame }.Concat(refinement.Attempts), "is5_sarkaz");
        Check(candidates.Count == 6, "retry diagnostics do not become extra frames or candidates");
        Check(candidates.Count(item => item.Label == "功名の石柱") == 2, "two distinct cards stay two");
        using var detail = JsonDocument.Parse(refinement.Frame.RecognitionDetailJson);
        var recovered = detail.RootElement.GetProperty("filtered")[2];
        Check(recovered.GetProperty("box")[0].GetInt32() == 900
            && recovered.GetProperty("box")[1].GetInt32() == 140, "original frame coordinates survive");
        Check(recovered.GetProperty("rhodes_name_retry").GetProperty("original_text").GetString()
            == "功名の石枝", "original OCR remains available for diagnosis");
        var loads = RhodesMaaThoughtLoadOcrExpander.BuildRequests(refinement.Frame.RecognitionDetailJson);
        var pillar = loads.Single(item => item.Entry.EndsWith("legacy_05", StringComparison.Ordinal));
        Check(pillar.X == 845 && pillar.Y == 200, "load crop uses the original title position");
        Check(RhodesMaaThoughtNameOcrExpander.BuildRequests(refinement.Frame).Count == 0,
            "resolved titles are not retried again");
        var overlapping = RhodesMaaLocalCandidateConverter.FromTaskResults(
            "is5ThoughtFull", new[] { refinement.Frame, refinement.Frame }, "is5_sarkaz");
        Check(overlapping.Count == candidates.Count, "overlapping captures do not double recovered cards");
    }

    public static void PreservesUnknownNamesAndRejectsUnrelatedEvidence()
    {
        var source = Frame(("枯れ木と若枝", 500, 140), ("走る都市", 500, 260), ("功名の石枝", 900, 140));
        foreach (var retry in new[]
        {
            RowResult("retry", "功名の石枝", 0.9999, 0, 0),
            RowResult("retry", "走る都市", 0.9999, 0, 0),
            RowResult("retry", "功名の石柱", 0.5, 0, 0),
            RowResult("retry", "功名の石柱", 0.99, 0, 0) with { Succeeded = false },
            RowResult("retry", "功名の石柱", 0.99, 0, 0) with { Hit = false },
            Frame(("功名の石柱", 0, 0), ("土を被った食器", 0, 30)),
        })
        {
            var refined = RhodesMaaThoughtNameOcrExpander.RefineAsync(source,
                (request, _) => Task.FromResult(retry with { Entry = request.Entry })).GetAwaiter().GetResult();
            Check(refined.Frame == source, "unconfirmed retries preserve the original frame");
        }

        foreach (var skipped in new[]
        {
            Frame(("功名の石枝", 900, 140)),
            Frame(("機械", 500, 140), ("功名の石枝", 900, 140)),
            Frame(("枯れ木と若枝", 500, 140), ("走る都市", 500, 260), ("築信", 900, 140)),
            Frame(("枯れ木と若枝", 500, 140), ("功名の石枝", 900, 175)),
            Frame(("枯れ木と若枝", 500, 140), ("功名の石枝", 650, 140)),
            Frame(("枯れ木と若枝", 500, 140), ("功名の石柱を所持", 900, 140)),
            Frame(("枯れ木と若枝", 500, 140), ("功名の石枝", 1270, 140)),
            source with { Entry = "RhodesOcrRegion_relic_list_text" },
            source with { RecognitionDetailJson = "invalid" },
            source with { RecognitionDetailJson = "[]" },
        })
            Check(RhodesMaaThoughtNameOcrExpander.BuildRequests(skipped).Count == 0, "unanchored or invalid title is skipped");

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var called = false;
        try
        {
            RhodesMaaThoughtNameOcrExpander.RefineAsync(source, (request, _) =>
            {
                called = true;
                return Task.FromResult(RowResult(request.Entry, "功名の石柱", 0.99, 0, 0));
            }, cancelled.Token).GetAwaiter().GetResult();
            throw new Exception("cancellation must stop retries");
        }
        catch (OperationCanceledException) { }
        Check(!called, "cancelled retry does not invoke OCR");
    }

    public static void BoundsRetriesAndPreservesRawEvidence()
    {
        var rows = new List<(string, int, int)>();
        foreach (var x in new[] { 500, 900 })
        {
            rows.Add(("枯れ木と若枝", x, 100));
            rows.Add(("走る都市", x, 164));
            for (var y = 228; y <= 548; y += 64)
                rows.Add(("功名の石枝", x, y));
        }
        var source = Frame(rows.ToArray());
        var requests = RhodesMaaThoughtNameOcrExpander.BuildRequests(source);
        Check(requests.Count == RhodesMaaThoughtNameOcrExpander.MaximumRetriesPerFrame,
            "large lists stop at the per-frame retry budget");

        var original = Frame(("枯れ木と若枝", 500, 140), ("功名の石枝", 900, 140), ("走る都市", 500, 260));
        using var detail = JsonDocument.Parse(original.RecognitionDetailJson);
        var all = detail.RootElement.GetProperty("filtered").Clone();
        source = original with { RecognitionDetailJson = JsonSerializer.Serialize(new { result = new { all, filtered = all } }) };
        var refined = RhodesMaaThoughtNameOcrExpander.RefineAsync(source,
            (request, _) => Task.FromResult(RowResult(request.Entry, "功名の石柱", 0.99, 0, 0)))
            .GetAwaiter().GetResult();
        using var after = JsonDocument.Parse(refined.Frame.RecognitionDetailJson);
        var root = after.RootElement.GetProperty("result");
        Check(root.GetProperty("all")[1].GetProperty("text").GetString() == "功名の石枝",
            "raw all results remain unchanged");
        Check(root.GetProperty("filtered")[1].GetProperty("text").GetString() == "功名の石柱",
            "nested primary results receive the verified title");
    }

    private static MaaTaskRunResult Frame(params (string Text, int X, int Y)[] rows) => new(
        "RhodesOcrRegion_is5_thought_list_text", "Succeeded", true, "synthetic OCR",
        JsonSerializer.Serialize(new { filtered = rows.Select(row => new
            { text = row.Text, score = 0.99, box = new[] { row.X, row.Y, 140, 20 } }) }), "OCR", true);

    private static MaaTaskRunResult RowResult(string entry, string text, double score, int x, int y) => new(
        entry, "Succeeded", true, "synthetic title OCR",
        JsonSerializer.Serialize(new { filtered = new[] { new { text, score, box = new[] { x, y, 560, 80 } } } }), "OCR", true);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
