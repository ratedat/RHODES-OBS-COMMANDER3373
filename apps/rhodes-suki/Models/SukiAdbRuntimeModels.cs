using MaaFramework.Binding;

namespace RhodesSuki.Models;

public sealed record RhodesMuMuCapabilitySnapshot(
    string EmulatorRoot,
    string ManagerPath,
    string IpcLibraryPath,
    string ManagerVersion,
    bool ScreenshotEnhancementAvailable,
    bool TouchEnhancementAvailable,
    int InstanceIndex,
    string ScreenshotDetail,
    string TouchDetail)
{
    public static RhodesMuMuCapabilitySnapshot NotDetected(string detail = "MuMuの実行環境を確認できませんでした。") =>
        new("", "", "", "", false, false, 0, detail, detail);
}

public sealed record RhodesMaaAdbOptionResolution(
    MaaSessionOptions Options,
    bool ScreenshotEnhancementActive,
    bool TouchEnhancementActive,
    string ScreenshotDetail,
    string TouchDetail);

public sealed record RhodesAdbBenchmarkResult(
    int AttemptCount,
    int SuccessCount,
    long MinimumMilliseconds,
    double AverageMilliseconds,
    long MaximumMilliseconds,
    int LastImageBytes,
    IReadOnlyList<string> Errors)
{
    public bool Succeeded => SuccessCount > 0;

    public string Summary => AttemptCount == 0
        ? "未実行"
        : $"成功 {SuccessCount}/{AttemptCount} · min/avg/max {MinimumMilliseconds}/{AverageMilliseconds:F1}/{MaximumMilliseconds} ms · {LastImageBytes:N0} bytes";
}

public sealed record SukiTouchRectangle(int X, int Y, int Width, int Height);

public sealed record SukiTouchPoint(int X, int Y);

public sealed record SukiTouchTestConfirmation(
    Guid Token,
    SukiTouchRectangle Rectangle,
    SukiTouchPoint Point,
    DateTimeOffset ExpiresAt,
    bool IsValid,
    string Detail);

public sealed record SukiTouchTestResult(bool Succeeded, string Detail, MaaJobStatus Status);

public sealed record RhodesAdbRecoveryStage(string Id, bool Succeeded, string Detail);

public sealed record RhodesAdbRecoveryResult(
    bool Succeeded,
    MaaSessionSnapshot Snapshot,
    IReadOnlyList<RhodesAdbRecoveryStage> Stages);

public sealed record RhodesAdbProcessInfo(int ProcessId, string ExecutablePath);

public sealed record RhodesAdbProcessActionResult(bool Succeeded, int AffectedCount, string Detail);

public sealed record RhodesManagedAdbInstallResult(bool Succeeded, string AdbPath, string Detail);
