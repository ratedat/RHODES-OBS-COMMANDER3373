using RhodesSuki.Services;

namespace RhodesSuki.Tests;

public static class RhodesRecognitionFrameWaiterTests
{
    public static void Run()
    {
        AcceptsChangedFramesAcrossCaptureDurations();
        AcceptsChangedFramesWithVariableCaptureDuration();
        RequiresTwoFreshUnchangedFramesAfterFixedDelay();
        FastCaptureStillReachesTheFixedDelayConfirmationWindow();
        RejectsAStalePostDelayFrameWhenMovementFollows();
        ContinuousMovementTimesOutWithoutResolving();
        TransientCaptureFailurePreservesUncertainty();
        RecognitionFailureDoesNotResolveTheSegment();
        CancellationInterruptsTheWait();
        CancellationAfterCaptureDoesNotContinueToFingerprint();
    }

    private static void AcceptsChangedFramesAcrossCaptureDurations()
    {
        foreach (var captureDurationMs in new[] { 32L, 48L, 64L, 80L })
        {
            var harness = new WaitHarness(
                [
                    Capture.Success("moving", 0xffff000000000000UL, captureDurationMs),
                    Capture.Success("changed", 0x00ffff0000000000UL, captureDurationMs),
                    Capture.Success("stable-1", 0x00ffff0000000001UL, captureDurationMs),
                    Capture.Success("stable-2", 0x00ffff0000000001UL, captureDurationMs),
                ]);

            var result = harness.Run();

            Equal(RhodesRecognitionFrameWaitOutcome.Accepted, result.Outcome, $"{captureDurationMs}ms capture outcome");
            Equal(true, result.SawViewportChange, $"{captureDurationMs}ms capture observes movement");
            Equal(true, result.CanResolveSegment, $"{captureDurationMs}ms capture resolves after recognition");
            Equal("stable-2", result.Frame, $"{captureDurationMs}ms capture returns the recognized stable frame");
        }
    }

    private static void AcceptsChangedFramesWithVariableCaptureDuration()
    {
        var harness = new WaitHarness(
        [
            Capture.Success("moving", 0xffff000000000000UL, 32),
            Capture.Success("changed", 0x00ffff0000000000UL, 80),
            Capture.Success("stable-1", 0x00ffff0000000001UL, 48),
            Capture.Success("stable-2", 0x00ffff0000000001UL, 64),
        ]);

        var result = harness.Run();

        Equal(RhodesRecognitionFrameWaitOutcome.Accepted, result.Outcome, "variable capture outcome");
        Equal(224L, result.Timing.CaptureMilliseconds, "variable capture timing is accumulated");
        Equal(4L, result.Timing.FingerprintMilliseconds, "fingerprint timing is accumulated for sampled frames");
        Equal(2L, result.Timing.RecognitionMilliseconds, "recognition timing is exposed");
    }

    private static void FastCaptureStillReachesTheFixedDelayConfirmationWindow()
    {
        var captures = Enumerable.Range(0, 13)
            .Select(index => Capture.Success($"fast-static-{index}", 0x1111111111111111UL, 1))
            .ToArray();
        var harness = new WaitHarness(captures, preSwipeFingerprint: 0x1111111111111111UL);

        var result = harness.Run();

        Equal(RhodesRecognitionFrameWaitOutcome.Accepted, result.Outcome, "fast capture reaches fixed-delay confirmation");
        Equal(true, result.PollCount > 8, "configured poll count cannot cut off the fixed-delay safety window");
        Equal(true, result.CanResolveSegment, "fast unchanged endpoint resolves after two fresh samples");
    }

    private static void RequiresTwoFreshUnchangedFramesAfterFixedDelay()
    {
        var harness = new WaitHarness(
        [
            Capture.Success("old-1", 0x1111111111111111UL, 32),
            Capture.Success("old-2", 0x1111111111111111UL, 32),
            Capture.Success("old-3", 0x1111111111111111UL, 32),
            Capture.Success("fresh-1", 0x1111111111111111UL, 32),
            Capture.Success("fresh-2", 0x1111111111111111UL, 32),
        ], preSwipeFingerprint: 0x1111111111111111UL);

        var result = harness.Run();

        Equal(RhodesRecognitionFrameWaitOutcome.Accepted, result.Outcome, "unchanged endpoint outcome");
        Equal(false, result.SawViewportChange, "unchanged endpoint remains distinct from movement");
        Equal(5, result.PollCount, "pre-delay stationary frames do not count toward the fresh confirmation");
        Equal("fresh-2", result.Frame, "second post-delay unchanged frame is recognized");
    }

    private static void RejectsAStalePostDelayFrameWhenMovementFollows()
    {
        var harness = new WaitHarness(
        [
            Capture.Success("old-1", 0x1111111111111111UL, 80),
            Capture.Success("stale-after-delay", 0x1111111111111111UL, 80),
            Capture.Success("moving", 0xffff000000000000UL, 32),
            Capture.Success("changed", 0x00ffff0000000000UL, 32),
            Capture.Success("stable-1", 0x00ffff0000000001UL, 32),
            Capture.Success("stable-2", 0x00ffff0000000001UL, 32),
        ], preSwipeFingerprint: 0x1111111111111111UL);

        var result = harness.Run();

        Equal(RhodesRecognitionFrameWaitOutcome.Accepted, result.Outcome, "stale frame followed by movement outcome");
        Equal(true, result.SawViewportChange, "later movement is retained");
        Equal("stable-2", result.Frame, "stale frame is not accepted before the second confirmation");
    }

    private static void ContinuousMovementTimesOutWithoutResolving()
    {
        var captures = Enumerable.Range(0, 8)
            .Select(index => Capture.Success($"moving-{index}", 0xffUL << (index * 8), 48))
            .ToArray();
        var harness = new WaitHarness(captures);

        var result = harness.Run();

        Equal(RhodesRecognitionFrameWaitOutcome.TimedOut, result.Outcome, "continuous movement times out");
        Equal(false, result.Stable, "continuous movement is not stable");
        Equal(false, result.CanResolveSegment, "continuous movement cannot resolve a segment");
        Equal(0, harness.RecognitionCalls, "unstable frames are never recognized");
    }

    private static void TransientCaptureFailurePreservesUncertainty()
    {
        var harness = new WaitHarness(
        [
            Capture.Failure(32),
            Capture.Success("changed-1", 0xffff000000000000UL, 48),
            Capture.Success("changed-2", 0xffff000000000000UL, 48),
            Capture.Success("stable", 0xffff000000000000UL, 48),
        ]);

        var result = harness.Run();

        Equal(RhodesRecognitionFrameWaitOutcome.Accepted, result.Outcome, "later stable frame can still be returned");
        Equal(true, result.HadCaptureFailure, "capture failure remains visible");
        Equal(false, result.CanResolveSegment, "capture uncertainty is not erased by a later success");
    }

    private static void RecognitionFailureDoesNotResolveTheSegment()
    {
        var harness = new WaitHarness(
        [
            Capture.Success("changed-1", 0xffff000000000000UL, 48),
            Capture.Success("changed-2", 0xffff000000000000UL, 48),
            Capture.Success("stable", 0xffff000000000000UL, 48),
        ], recognitionSucceeds: false);

        var result = harness.Run();

        Equal(RhodesRecognitionFrameWaitOutcome.RecognitionFailed, result.Outcome, "recognition failure outcome");
        Equal(true, result.Stable, "recognition failure does not rewrite stability evidence");
        Equal(false, result.RecognitionSucceeded, "recognition failure remains explicit");
        Equal(false, result.CanResolveSegment, "failed recognition cannot resolve a segment");
        Equal(1, harness.RecognitionCalls, "stable frame is recognized exactly once");
    }

    private static void CancellationInterruptsTheWait()
    {
        using var cancellation = new CancellationTokenSource();
        var harness = new WaitHarness(
        [
            Capture.Success("moving-1", 0xffff000000000000UL, 32),
            Capture.Success("moving-2", 0x00ffff0000000000UL, 32),
        ], cancelDuringFirstDelay: cancellation);

        try
        {
            harness.Run(cancellation.Token);
            throw new InvalidOperationException("cancellation should throw");
        }
        catch (OperationCanceledException)
        {
        }

        Equal(0, harness.RecognitionCalls, "cancelled wait does not recognize a frame");
    }

    private static void CancellationAfterCaptureDoesNotContinueToFingerprint()
    {
        using var cancellation = new CancellationTokenSource();
        var harness = new WaitHarness(
            [Capture.Success("cancelled-capture", 0xffff000000000000UL, 32)],
            cancelAfterFirstCapture: cancellation);

        try
        {
            harness.Run(cancellation.Token);
            throw new InvalidOperationException("post-capture cancellation should throw");
        }
        catch (OperationCanceledException)
        {
        }

        Equal(0, harness.FingerprintCalls, "cancelled native capture does not continue to fingerprint");
        Equal(0, harness.RecognitionCalls, "cancelled native capture does not continue to recognition");
    }

    private sealed class WaitHarness
    {
        private readonly Queue<Capture> _captures;
        private readonly ulong _preSwipeFingerprint;
        private readonly bool _recognitionSucceeds;
        private readonly CancellationTokenSource? _cancelDuringFirstDelay;
        private readonly CancellationTokenSource? _cancelAfterFirstCapture;
        private int _captureCalls;

        public WaitHarness(
            IEnumerable<Capture> captures,
            ulong preSwipeFingerprint = 0,
            bool recognitionSucceeds = true,
            CancellationTokenSource? cancelDuringFirstDelay = null,
            CancellationTokenSource? cancelAfterFirstCapture = null)
        {
            _captures = new Queue<Capture>(captures);
            _preSwipeFingerprint = preSwipeFingerprint;
            _recognitionSucceeds = recognitionSucceeds;
            _cancelDuringFirstDelay = cancelDuringFirstDelay;
            _cancelAfterFirstCapture = cancelAfterFirstCapture;
        }

        public int RecognitionCalls { get; private set; }

        public int FingerprintCalls { get; private set; }

        public RhodesRecognitionFrameWaitResult<string> Run(CancellationToken cancellationToken = default) =>
            RhodesRecognitionFrameWaiter.WaitAsync(
                _preSwipeFingerprint,
                new RhodesRecognitionFrameWaitOptions(
                    FixedDelayMilliseconds: 160,
                    PollIntervalMilliseconds: 16,
                    StableSampleCount: 2,
                    MaxPollCount: 8),
                CaptureAsync,
                FingerprintAsync,
                RecognizeAsync,
                DelayAsync,
                cancellationToken: cancellationToken).GetAwaiter().GetResult();

        private ValueTask<RhodesRecognitionFrameCapture<string>> CaptureAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_captures.Count == 0)
                return ValueTask.FromResult(RhodesRecognitionFrameCapture<string>.Failure("no more captures", 0));
            var next = _captures.Dequeue();
            _captureCalls++;
            if (_captureCalls == 1)
                _cancelAfterFirstCapture?.Cancel();
            return ValueTask.FromResult(next.Succeeded
                ? RhodesRecognitionFrameCapture<string>.Success(next.Frame, next.CaptureMilliseconds)
                : RhodesRecognitionFrameCapture<string>.Failure("capture failed", next.CaptureMilliseconds));
        }

        private ValueTask<RhodesRecognitionTimedFingerprint> FingerprintAsync(
            string frame,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FingerprintCalls++;
            var capture = Capture.ByFrame[frame];
            return ValueTask.FromResult(new RhodesRecognitionTimedFingerprint(capture.Fingerprint, 1));
        }

        private ValueTask<RhodesRecognitionFrameRecognition> RecognizeAsync(
            string frame,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RecognitionCalls++;
            return ValueTask.FromResult(new RhodesRecognitionFrameRecognition(
                _recognitionSucceeds,
                _recognitionSucceeds,
                _recognitionSucceeds ? "recognized" : "recognition failed",
                2));
        }

        private ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            if (_cancelDuringFirstDelay is not null)
            {
                _cancelDuringFirstDelay.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return ValueTask.CompletedTask;
        }
    }

    private sealed record Capture(
        bool Succeeded,
        string Frame,
        ulong Fingerprint,
        long CaptureMilliseconds)
    {
        public static Dictionary<string, Capture> ByFrame { get; } = new(StringComparer.Ordinal);

        public static Capture Success(string frame, ulong fingerprint, long captureMilliseconds)
        {
            var capture = new Capture(true, frame, fingerprint, captureMilliseconds);
            ByFrame[frame] = capture;
            return capture;
        }

        public static Capture Failure(long captureMilliseconds) =>
            new(false, "", 0, captureMilliseconds);
    }

    private static void Equal<T>(T expected, T actual, string label)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected={expected}, actual={actual}");
    }
}
