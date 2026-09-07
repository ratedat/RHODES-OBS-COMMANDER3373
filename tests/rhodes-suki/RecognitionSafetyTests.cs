using RhodesSuki.Models;
using RhodesSuki.Services;
using SkiaSharp;

namespace RhodesSuki.Tests;

public static class RecognitionSafetyTests
{
    public static void Run()
    {
        EmptyTrackerDoesNotConfirmAnUnverifiedZeroCount();
        ExhaustedUnresolvedCardsRemainVisibleToTheCaller();
        NewTrackerRechecksSameCountOperatorMetadata();
        EndpointCacheClearInvalidatesEveryRememberedContext();
    }

    private static void EmptyTrackerDoesNotConfirmAnUnverifiedZeroCount()
    {
        var tracker = new RhodesOperatorScanTracker();

        Equal(false, tracker.AllCardsResolved, "empty tracker is not authoritative zero evidence");
        Equal(0, tracker.UnresolvedCardCount, "empty tracker has no observed unresolved cards");
        Equal(0, tracker.ExhaustedUnresolvedCount, "empty tracker has no exhausted cards");
        Equal(false, tracker.CanStopScan, "empty tracker cannot complete a scan");
    }

    private static void ExhaustedUnresolvedCardsRemainVisibleToTheCaller()
    {
        using var frame = CreateOperatorFrame();
        var encoded = EncodePng(frame);
        var requests = Requests();
        var tracker = new RhodesOperatorScanTracker(maxAttemptsPerCard: 2);

        var first = tracker.Select(encoded, requests);
        var unmatchedHit = UnmatchedOperatorNameHit();
        var firstResolved = RhodesMaaLocalCandidateConverter
            .FromTaskResults("operatorsFull", [unmatchedHit])
            .Any(candidate => candidate.Kind.Equals("operator", StringComparison.OrdinalIgnoreCase));
        Equal(true, unmatchedHit.Hit, "MAA reports a name hit");
        Equal(false, firstResolved, "unregistered hit does not resolve to an operator");
        tracker.RecordResult(first.WorkItems.Single().TrackingId, firstResolved);
        var repeated = tracker.Select(encoded, requests);
        tracker.RecordResult(repeated.WorkItems.Single().TrackingId, firstResolved);

        Equal(true, tracker.CanStopCurrentViewport, "retry exhaustion preserves the existing bounded viewport stop");
        Equal(true, tracker.CanStopScan, "retry exhaustion preserves the existing bounded scan stop");
        Equal(false, tracker.AllCardsResolved, "an OCR hit without a mapped operator is not complete");
        Equal(1, tracker.UnresolvedCardCount, "unmapped operator remains unresolved");
        Equal(1, tracker.ExhaustedUnresolvedCount, "caller can surface exhausted uncertainty");
    }

    private static void NewTrackerRechecksSameCountOperatorMetadata()
    {
        using var frame = CreateOperatorFrame();
        var encoded = EncodePng(frame);
        var requests = Requests();
        var firstRun = new RhodesOperatorScanTracker();

        var initial = firstRun.Select(encoded, requests);
        firstRun.RecordResult(initial.WorkItems.Single().TrackingId, resolved: true, "amiya");
        Equal(0, firstRun.Select(encoded, requests).WorkItems.Count, "resolved card is reused only inside one scan");

        var nextRun = new RhodesOperatorScanTracker();
        var refreshed = nextRun.Select(encoded, requests);

        Equal(1, refreshed.WorkItems.Count, "new scan rechecks Amiya role and promotion even when roster count is unchanged");
        Equal(false, nextRun.AllCardsResolved, "fresh metadata work must complete before the new scan is resolved");
        nextRun.RecordResult(refreshed.WorkItems.Single().TrackingId, resolved: true, "amiya2");
        Equal(true, nextRun.AllCardsResolved, "new scan can resolve the changed Amiya profession independently");
    }

    private static void EndpointCacheClearInvalidatesEveryRememberedContext()
    {
        var cache = new RhodesRecognitionEndpointCache();
        var passes = Passes();
        var relicPasses = passes.Select(pass => pass with { Axis = "vertical", Direction = pass.Direction == "left" ? "up" : "down" }).ToArray();
        const ulong operatorFingerprint = 0x1111111111111111UL;
        const ulong relicFingerprint = 0x2222222222222222UL;
        cache.RecordEndpoint("operatorsFull", "is5_sarkaz", "pc-framepool", 1280, 720, "left", operatorFingerprint);
        cache.RecordEndpoint("relicsFull", "is6_sui", "pc-framepool", 1280, 720, "up", relicFingerprint);

        Equal(true, cache.Select("operatorsFull", "is5_sarkaz", "pc-framepool", 1280, 720, operatorFingerprint, passes).UsedRememberedEndpoint,
            "operator endpoint is remembered before a run context change");
        Equal(true, cache.Select("relicsFull", "is6_sui", "pc-framepool", 1280, 720, relicFingerprint, relicPasses).UsedRememberedEndpoint,
            "relic endpoint is remembered before a run context change");

        cache.Clear();

        Equal(false, cache.Select("operatorsFull", "is5_sarkaz", "pc-framepool", 1280, 720, operatorFingerprint, passes).UsedRememberedEndpoint,
            "run or target context clear invalidates operator endpoint");
        Equal(false, cache.Select("relicsFull", "is6_sui", "pc-framepool", 1280, 720, relicFingerprint, relicPasses).UsedRememberedEndpoint,
            "run or target context clear invalidates relic endpoint");
    }

    private static MaaDynamicOcrRequest[] Requests() =>
    [
        new MaaDynamicOcrRequest("operator.card.name.0", 100, 100, 100, 40, 4, 0.9),
    ];

    private static MaaTaskRunResult UnmatchedOperatorNameHit() => new(
        "operator.card.name.0",
        "Succeeded",
        true,
        "unregistered name",
        """{"filtered_results":[{"text":"完全未登録名3373","score":0.99}]}""",
        "OCR",
        true);

    private static SKBitmap CreateOperatorFrame()
    {
        var frame = new SKBitmap(1280, 720);
        frame.Erase(SKColors.Black);
        using var paint = new SKPaint { Color = SKColors.White };
        using var canvas = new SKCanvas(frame);
        canvas.DrawRect(new SKRect(110, 105, 142, 134), paint);
        return frame;
    }

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static RhodesRecognitionScrollPass[] Passes() =>
    [
        new RhodesRecognitionScrollPass(
            "horizontal",
            "right",
            "right",
            new RhodesRecognitionSwipeArea(0, 0, 1, 1),
            new RhodesRecognitionSwipeArea(1, 0, 1, 1),
            1,
            1,
            1,
            1,
            0,
            0,
            true,
            false),
        new RhodesRecognitionScrollPass(
            "horizontal",
            "left",
            "left",
            new RhodesRecognitionSwipeArea(1, 0, 1, 1),
            new RhodesRecognitionSwipeArea(0, 0, 1, 1),
            1,
            1,
            1,
            1,
            0,
            0,
            true,
            false),
    ];

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected={expected}, actual={actual}");
    }
}
