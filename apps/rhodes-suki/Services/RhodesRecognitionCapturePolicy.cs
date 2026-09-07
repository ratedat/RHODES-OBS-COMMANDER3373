using MaaFramework.Binding;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionCapturePolicy
{
    // Detail probes may navigate and replace the current capture. Keep those outside the waiter.
    public static bool CanRecognizeDuringSettle(string profileId, bool collectCandidates) =>
        collectCandidates && profileId is "operatorsFull" or "relicsFull" or "is5ThoughtFull";

    public static bool ShouldUseAdaptivePcSettle(
        bool isPcConnectionTargetSelected,
        bool isControllerReady,
        MaaSessionControllerKind controllerKind,
        Win32ScreencapMethods? activeScreencapMethod) =>
        isPcConnectionTargetSelected
        && isControllerReady
        && controllerKind == MaaSessionControllerKind.Win32
        && activeScreencapMethod == Win32ScreencapMethods.FramePool;

    public static bool ShouldPersistAdaptiveScrollFrame(
        bool isFinalFrame,
        bool hadFailure) =>
        isFinalFrame || hadFailure;

    public static int MinimumStableElapsedMs(
        bool sawViewportChange,
        int fixedDelayMs) =>
        sawViewportChange
            ? 32
            : Math.Max(32, fixedDelayMs);

    public static int AdaptiveSettleBudgetMs(
        int fixedDelayMs,
        int pollIntervalMs) =>
        Math.Max(
            64,
            Math.Max(0, fixedDelayMs) + (Math.Max(1, pollIntervalMs) * 4));

    public static int AdaptiveSettlePollLimit(
        int fixedDelayMs,
        int pollIntervalMs,
        int stableSampleCount,
        int configuredMaximum)
    {
        var required = (long)DivideRoundUp(
                Math.Max(0, fixedDelayMs),
                Math.Max(1, pollIntervalMs))
            + Math.Max(1, stableSampleCount)
            + 1;
        return (int)Math.Min(
            int.MaxValue,
            Math.Max(Math.Max(1, configuredMaximum), required));
    }

    public static bool IsConfirmedScrollEndpoint(
        bool endpointOptimizationEnabled,
        bool sawViewportChange) =>
        !endpointOptimizationEnabled || sawViewportChange;

    public static bool CanAcceptStableFrame(
        bool settled,
        bool sawViewportChange,
        long elapsedMilliseconds,
        int fixedDelayMs) =>
        settled
        && elapsedMilliseconds >= MinimumStableElapsedMs(
            sawViewportChange,
            fixedDelayMs);

    private static int DivideRoundUp(int value, int divisor) =>
        value == 0 ? 0 : 1 + ((value - 1) / divisor);
}
