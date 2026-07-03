using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesSukiAdbDetectionSnapshot(
    string AdbPath,
    string AdbSerial,
    IReadOnlyList<MaaAdbPathCandidatePreview> AdbCandidates,
    MaaAdbPathCandidatePreview? SelectedAdbPathCandidate,
    IReadOnlyList<MaaAdbDevicePreview> Devices,
    string DetectionSummary,
    string DetectionDetail,
    string SessionState,
    string SessionDetail,
    string StatusMessage);

public static class RhodesSukiAdbDetectionWorkflow
{
    public static string ResolveDetectedPresetId(string? currentPresetId, MaaAdbPathCandidatePreview? selectedCandidate)
    {
        var current = NormalizeCurrentPresetId(currentPresetId);
        var detected = NormalizeDetectedPresetId(selectedCandidate?.Preset);
        if (string.IsNullOrWhiteSpace(detected) || detected.Equals("custom", StringComparison.OrdinalIgnoreCase))
            return current;

        return current is "auto" or "custom" ? detected : current;
    }

    public static async Task<RhodesSukiAdbDetectionSnapshot> DetectAsync(
        RhodesAdbApiSettings settings,
        Func<RhodesAdbApiSettings, CancellationToken, Task<RhodesAdbLocalDetectionResult>>? detectAsync = null,
        Func<string, bool>? fileExists = null,
        CancellationToken cancellationToken = default)
    {
        detectAsync ??= (current, token) => RhodesAdbLocalDetector.DetectAsync(current, cancellationToken: token);
        var detection = await detectAsync(settings, cancellationToken);
        var adbPath = FirstNonEmpty(detection.RuntimeAdbPath, detection.SelectedAdbPath, settings.AdbPath, "adb");
        var serial = FirstNonEmpty(detection.RuntimeSerial, settings.Serial);
        var candidates = RhodesAdbCandidateRegistry.Normalize(detection.AdbCandidates, fileExists);
        var selected = RhodesAdbCandidateRegistry.SelectDefault(candidates, adbPath);
        var connectDetail = ConnectDetail(detection.Connect);
        var detail = string.IsNullOrWhiteSpace(detection.Error)
            ? $"選択中: {adbPath} / {(string.IsNullOrWhiteSpace(serial) ? "serial未選択" : serial)}{connectDetail}"
            : $"{detection.Error}{connectDetail}";

        return new RhodesSukiAdbDetectionSnapshot(
            adbPath,
            serial,
            candidates,
            selected,
            detection.Devices,
            $"Sukiローカル検出: ADB候補{candidates.Count}件 / 端末{detection.Devices.Count}件",
            detail,
            detection.Succeeded ? "ADB検出OK" : "ADB検出失敗",
            detail,
            detection.Devices.Count == 0
                ? $"ADB端末は見つかりませんでした。{detail}"
                : $"Sukiローカル検出で端末を取得しました: {detection.Devices.Count}件");
    }

    private static string ConnectDetail(RhodesAdbConnectPreview? connect)
    {
        if (connect is null)
            return "";
        return connect.Succeeded
            ? $" / connect {connect.Address}"
            : $" / connect失敗 {connect.Address}: {connect.Detail}";
    }

    private static string FirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }
        return "";
    }

    private static string NormalizeCurrentPresetId(string? value)
    {
        var normalized = string.IsNullOrWhiteSpace(value) ? "auto" : value.Trim().ToLowerInvariant();
        return normalized;
    }

    private static string NormalizeDetectedPresetId(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();
    }
}
