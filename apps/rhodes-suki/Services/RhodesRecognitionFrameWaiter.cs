namespace RhodesSuki.Services;

public sealed record RhodesRecognitionFrameWaitOptions(
    int FixedDelayMilliseconds,
    int PollIntervalMilliseconds,
    int StableSampleCount = 2,
    int MaxPollCount = 8);

public sealed record RhodesRecognitionFrameCapture<TFrame>(
    bool Succeeded,
    TFrame? Frame,
    string Detail,
    long ElapsedMilliseconds)
    where TFrame : class
{
    public static RhodesRecognitionFrameCapture<TFrame> Success(
        TFrame frame,
        long elapsedMilliseconds) =>
        new(true, frame, "", Math.Max(0, elapsedMilliseconds));

    public static RhodesRecognitionFrameCapture<TFrame> Failure(
        string detail,
        long elapsedMilliseconds) =>
        new(false, null, detail, Math.Max(0, elapsedMilliseconds));
}

public sealed record RhodesRecognitionTimedFingerprint(
    ulong Value,
    long ElapsedMilliseconds);

public sealed record RhodesRecognitionFrameRecognition(
    bool Succeeded,
    bool Hit,
    string Detail,
    long ElapsedMilliseconds);

public sealed record RhodesRecognitionFrameWaitTiming(
    long CaptureMilliseconds,
    long FingerprintMilliseconds,
    long RecognitionMilliseconds,
    long DelayMilliseconds);

public enum RhodesRecognitionFrameWaitOutcome
{
    Accepted,
    TimedOut,
    CaptureFailed,
    RecognitionFailed,
}

public sealed record RhodesRecognitionFrameWaitResult<TFrame>(
    RhodesRecognitionFrameWaitOutcome Outcome,
    TFrame? Frame,
    bool Stable,
    bool SawViewportChange,
    bool RecognitionSucceeded,
    bool RecognitionHit,
    bool HadCaptureFailure,
    int PollCount,
    string Detail,
    RhodesRecognitionFrameWaitTiming Timing)
    where TFrame : class
{
    public bool CanResolveSegment =>
        Outcome == RhodesRecognitionFrameWaitOutcome.Accepted
        && Stable
        && RecognitionSucceeded
        && !HadCaptureFailure;
}

public static class RhodesRecognitionFrameWaiter
{
    public static async Task<RhodesRecognitionFrameWaitResult<TFrame>> WaitAsync<TFrame>(
        ulong preSwipeFingerprint,
        RhodesRecognitionFrameWaitOptions options,
        Func<CancellationToken, ValueTask<RhodesRecognitionFrameCapture<TFrame>>> captureAsync,
        Func<TFrame, CancellationToken, ValueTask<RhodesRecognitionTimedFingerprint>> fingerprintAsync,
        Func<TFrame, CancellationToken, ValueTask<RhodesRecognitionFrameRecognition>> recognizeAsync,
        Func<TimeSpan, CancellationToken, ValueTask> delayAsync,
        Action<string, double>? performanceObserver = null,
        CancellationToken cancellationToken = default)
        where TFrame : class
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(captureAsync);
        ArgumentNullException.ThrowIfNull(fingerprintAsync);
        ArgumentNullException.ThrowIfNull(recognizeAsync);
        ArgumentNullException.ThrowIfNull(delayAsync);
        var fixedDelayMilliseconds = Math.Max(0, options.FixedDelayMilliseconds);
        var pollIntervalMilliseconds = Math.Max(1, options.PollIntervalMilliseconds);
        var effectiveMaxPollCount = RhodesRecognitionCapturePolicy.AdaptiveSettlePollLimit(
            fixedDelayMilliseconds,
            pollIntervalMilliseconds,
            options.StableSampleCount,
            options.MaxPollCount);
        var tracker = new RhodesRecognitionFrameSettleTracker(
            preSwipeFingerprint,
            options.StableSampleCount);
        RhodesRecognitionFrameSettleTracker? unchangedAfterDelayTracker = null;
        TFrame? lastSuccessfulFrame = null;
        var pollCount = 0;
        var successfulCaptureCount = 0;
        var hadCaptureFailure = false;
        long elapsedMilliseconds = 0;
        long captureMilliseconds = 0;
        long fingerprintMilliseconds = 0;
        long recognitionMilliseconds = 0;
        long delayMilliseconds = 0;

        for (; pollCount < effectiveMaxPollCount; pollCount++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (pollCount > 0 && pollIntervalMilliseconds > 0)
            {
                await delayAsync(
                    TimeSpan.FromMilliseconds(pollIntervalMilliseconds),
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                delayMilliseconds += pollIntervalMilliseconds;
                elapsedMilliseconds += pollIntervalMilliseconds;
            }

            var capture = await captureAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            captureMilliseconds += Math.Max(0, capture.ElapsedMilliseconds);
            elapsedMilliseconds += Math.Max(0, capture.ElapsedMilliseconds);
            performanceObserver?.Invoke("capture", Math.Max(0, capture.ElapsedMilliseconds));
            if (!capture.Succeeded || capture.Frame is null)
            {
                hadCaptureFailure = true;
                continue;
            }

            successfulCaptureCount++;
            lastSuccessfulFrame = capture.Frame;
            var fingerprint = await fingerprintAsync(capture.Frame, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            fingerprintMilliseconds += Math.Max(0, fingerprint.ElapsedMilliseconds);
            elapsedMilliseconds += Math.Max(0, fingerprint.ElapsedMilliseconds);
            performanceObserver?.Invoke("fingerprint", Math.Max(0, fingerprint.ElapsedMilliseconds));

            var settled = tracker.Observe(fingerprint.Value);
            if (!tracker.SawViewportChange)
            {
                if (elapsedMilliseconds < fixedDelayMilliseconds)
                    continue;

                unchangedAfterDelayTracker ??= new RhodesRecognitionFrameSettleTracker(
                    preSwipeFingerprint,
                    options.StableSampleCount);
                settled = unchangedAfterDelayTracker.Observe(fingerprint.Value);
            }

            if (!RhodesRecognitionCapturePolicy.CanAcceptStableFrame(
                    settled,
                    tracker.SawViewportChange,
                    elapsedMilliseconds,
                    fixedDelayMilliseconds))
            {
                continue;
            }

            var recognition = await recognizeAsync(capture.Frame, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            recognitionMilliseconds += Math.Max(0, recognition.ElapsedMilliseconds);
            performanceObserver?.Invoke("recognition", Math.Max(0, recognition.ElapsedMilliseconds));
            var timing = new RhodesRecognitionFrameWaitTiming(
                captureMilliseconds,
                fingerprintMilliseconds,
                recognitionMilliseconds,
                delayMilliseconds);
            if (!recognition.Succeeded)
            {
                return new RhodesRecognitionFrameWaitResult<TFrame>(
                    RhodesRecognitionFrameWaitOutcome.RecognitionFailed,
                    capture.Frame,
                    true,
                    tracker.SawViewportChange,
                    false,
                    recognition.Hit,
                    hadCaptureFailure,
                    pollCount + 1,
                    recognition.Detail,
                    timing);
            }

            return new RhodesRecognitionFrameWaitResult<TFrame>(
                RhodesRecognitionFrameWaitOutcome.Accepted,
                capture.Frame,
                true,
                tracker.SawViewportChange,
                true,
                recognition.Hit,
                hadCaptureFailure,
                pollCount + 1,
                recognition.Detail,
                timing);
        }

        var outcome = successfulCaptureCount == 0
            ? RhodesRecognitionFrameWaitOutcome.CaptureFailed
            : RhodesRecognitionFrameWaitOutcome.TimedOut;
        return new RhodesRecognitionFrameWaitResult<TFrame>(
            outcome,
            lastSuccessfulFrame,
            false,
            tracker.SawViewportChange,
            false,
            false,
            hadCaptureFailure,
            pollCount,
            outcome == RhodesRecognitionFrameWaitOutcome.CaptureFailed
                ? "安定待機中に画像を取得できませんでした。"
                : "安定した連続Frameを確認できませんでした。",
            new RhodesRecognitionFrameWaitTiming(
                captureMilliseconds,
                fingerprintMilliseconds,
                recognitionMilliseconds,
                delayMilliseconds));
    }
}
