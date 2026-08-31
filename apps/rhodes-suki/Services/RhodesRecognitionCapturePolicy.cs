using MaaFramework.Binding;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionCapturePolicy
{
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
}
