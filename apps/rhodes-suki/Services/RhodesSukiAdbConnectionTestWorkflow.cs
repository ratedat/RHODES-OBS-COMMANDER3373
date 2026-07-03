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
        return snapshot.IsReady
            ? new RhodesSukiAdbConnectionTestSnapshot("ADB接続OK", snapshot.Detail, "MAA Controller でADB接続を確認しました。")
            : new RhodesSukiAdbConnectionTestSnapshot("ADB接続テスト失敗", snapshot.Detail, $"ADB接続テスト失敗: {snapshot.Detail}");
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
}
