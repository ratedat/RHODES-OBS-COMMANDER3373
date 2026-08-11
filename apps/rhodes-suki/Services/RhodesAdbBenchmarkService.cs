using System.Diagnostics;
using MaaFramework.Binding;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesAdbBenchmarkService
{
    public static async Task<RhodesAdbBenchmarkResult> RunAsync(
        int attemptCount,
        Func<CancellationToken, Task<MaaCaptureResult>> captureAsync,
        Func<long>? getTimestampMilliseconds = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(captureAsync);
        attemptCount = Math.Clamp(attemptCount, 1, 20);
        Func<long> clock = getTimestampMilliseconds
            ?? (() => Stopwatch.GetTimestamp() * 1000L / Stopwatch.Frequency);
        var durations = new List<long>(attemptCount);
        var errors = new List<string>();
        var successCount = 0;
        var lastImageBytes = 0;
        for (var index = 0; index < attemptCount; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var startedAt = clock();
            MaaCaptureResult capture;
            try
            {
                capture = await captureAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                capture = new MaaCaptureResult(MaaJobStatus.Failed.ToString(), false, ex.Message, []);
            }
            var elapsed = Math.Max(0, clock() - startedAt);
            durations.Add(elapsed);
            if (capture.Succeeded)
            {
                successCount++;
                lastImageBytes = capture.EncodedImage.Length;
            }
            else
            {
                errors.Add($"#{index + 1}: {capture.Detail}");
            }
        }

        return new RhodesAdbBenchmarkResult(
            attemptCount,
            successCount,
            durations.Min(),
            durations.Average(),
            durations.Max(),
            lastImageBytes,
            errors);
    }
}
