using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesSukiAdbConnectionTestSnapshot(
    string SessionState,
    string SessionDetail,
    string StatusMessage);

public static class RhodesSukiAdbConnectionTestWorkflow
{
    public static RhodesSukiAdbConnectionTestSnapshot NoAvailableAdb(string detectionDetail)
    {
        var detail = string.IsNullOrWhiteSpace(detectionDetail) ? "ADB候補なし" : detectionDetail.Trim();
        return new RhodesSukiAdbConnectionTestSnapshot(
            "ADB接続テスト失敗",
            detail,
            $"利用可能なADBが見つかりません: {detail}");
    }

    public static RhodesSukiAdbConnectionTestSnapshot FromController(MaaSessionSnapshot snapshot)
    {
        if (snapshot.IsReady)
            return new RhodesSukiAdbConnectionTestSnapshot("ADB接続OK", snapshot.Detail, "MAA Controller でADB接続を確認しました。");

        var detail = string.IsNullOrWhiteSpace(snapshot.Detail) ? snapshot.State.Trim() : snapshot.Detail.Trim();
        if (IsFrameworkRuntimeFailure(snapshot))
        {
            return new RhodesSukiAdbConnectionTestSnapshot(
                "MAAFramework未準備",
                detail,
                $"ADB接続前にMAAFramework runtimeを確認してください: {detail}");
        }

        return new RhodesSukiAdbConnectionTestSnapshot("ADB接続テスト失敗", detail, $"ADB接続テスト失敗: {detail}");
    }

    public static RhodesSukiAdbConnectionTestSnapshot FromCapture(
        MaaCaptureResult? capture,
        string adbSerial,
        string capturePixelSizeLabel,
        string fallbackDetail)
    {
        if (capture?.Succeeded == true)
        {
            return new RhodesSukiAdbConnectionTestSnapshot(
                "ADB接続OK",
                $"{adbSerial.Trim()} / {capturePixelSizeLabel}",
                $"ADB接続テスト成功: {capturePixelSizeLabel}");
        }

        var detail = string.IsNullOrWhiteSpace(capture?.Detail) ? fallbackDetail.Trim() : capture.Detail.Trim();
        return new RhodesSukiAdbConnectionTestSnapshot(
            "ADB接続OK / 撮影失敗",
            detail,
            $"ADB接続は成功しましたが撮影に失敗しました: {detail}");
    }

    private static bool IsFrameworkRuntimeFailure(MaaSessionSnapshot snapshot)
    {
        var text = $"{snapshot.State} {snapshot.Detail}";
        return text.Contains("MAAFramework", StringComparison.OrdinalIgnoreCase)
            || text.Contains("ネイティブ", StringComparison.Ordinal)
            || text.Contains("VC++", StringComparison.OrdinalIgnoreCase)
            || text.Contains("DLL was not found", StringComparison.OrdinalIgnoreCase)
            || text.Contains("DllNotFound", StringComparison.OrdinalIgnoreCase);
    }
}
