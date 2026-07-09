using System.Text;

namespace RhodesSuki.Services;

/// <summary>
/// ランタイム画面の段階診断行を組み立てる。
/// 状態は `未確認`, `OK`, `注意`, `失敗` の4値で、各行に次に取る行動を1行で添える。
/// </summary>
public static class RhodesAdbDiagnosticsChecklist
{
    public const string StateUnknown = "未確認";
    public const string StateOk = "OK";
    public const string StateWarning = "注意";
    public const string StateFailed = "失敗";

    public static IReadOnlyList<SukiAdbDiagnosticStepRow> Build(RhodesAdbDiagnosticsInput input)
    {
        var adbPathStep = BuildAdbPathStep(input);
        var adbLaunchStep = BuildAdbLaunchStep(input, adbPathStep);
        var deviceStep = BuildDeviceStep(input, adbLaunchStep);
        var maaStep = BuildMaaStep(input);
        var captureStep = BuildCaptureStep(input);
        var resolutionStep = BuildResolutionStep(input);
        return [adbPathStep, adbLaunchStep, deviceStep, maaStep, captureStep, resolutionStep];
    }

    public static string BuildCopyText(
        RhodesAdbDiagnosticsCopyInput input,
        IEnumerable<SukiAdbDiagnosticStepRow> steps)
    {
        var builder = new StringBuilder();
        builder.AppendLine("RHODES OBS COMMANDER3373 ADB診断");
        builder.AppendLine($"IS: {TextOrFallback(input.Campaign, "IS未選択")}");
        builder.AppendLine($"ADB preset: {TextOrFallback(input.AdbPreset, "未選択")}");
        builder.AppendLine($"ADB path: {TextOrFallback(input.AdbPath, "未設定")}");
        builder.AppendLine($"serial: {TextOrFallback(input.AdbSerial, "未選択")}");
        builder.AppendLine($"撮影方式: {TextOrFallback(input.ScreencapMethod, "未選択")} ({TextOrFallback(input.ScreencapMethodId, "-")})");
        builder.AppendLine($"入力方式: {TextOrFallback(input.InputMethod, "未選択")} ({TextOrFallback(input.InputMethodId, "-")})");
        builder.AppendLine($"OCR: {TextOrFallback(input.OcrEngine, "未選択")} ({TextOrFallback(input.OcrEngineId, "-")})");
        builder.AppendLine($"capture: {TextOrFallback(input.CaptureSize, "未取得")} / {TextOrFallback(input.CaptureDetail, "-")}");
        builder.AppendLine($"MAA: {TextOrFallback(input.MaaState, "未確認")} / {TextOrFallback(input.SessionDetail, "-")}");
        builder.AppendLine($"ADB detection: {TextOrFallback(input.DetectionSummary, "未検出")} / {TextOrFallback(input.DetectionDetail, "-")}");
        builder.AppendLine("steps:");
        foreach (var step in steps.OrderBy(step => step.Order))
        {
            builder.Append("- ");
            builder.Append(step.Label);
            builder.Append(" [");
            builder.Append(step.State);
            builder.Append("] ");
            builder.Append(step.Detail);
            if (step.HasNextAction)
            {
                builder.Append(" / next: ");
                builder.Append(step.NextAction);
            }
            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private static string TextOrFallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static SukiAdbDiagnosticStepRow BuildAdbPathStep(RhodesAdbDiagnosticsInput input)
    {
        var path = input.AdbPath?.Trim() ?? "";
        if (path.Length == 0)
        {
            return Row(1, "ADB実行ファイル", StateFailed, "ADBパスが未設定です。",
                "プリセットを適用するか、参照からadb.exeを選択してください。");
        }

        var isBareCommand = !path.Contains('\\') && !path.Contains('/');
        if (isBareCommand)
        {
            return input.HasAvailableAdb
                ? Row(1, "ADB実行ファイル", StateOk, $"コマンド '{path}' (検出候補あり)", "")
                : Row(1, "ADB実行ファイル", StateUnknown, $"コマンド '{path}' はPATH解決に依存します。",
                    "自動検出を実行して候補から選ぶと確実です。");
        }

        return File.Exists(path)
            ? Row(1, "ADB実行ファイル", StateOk, path, "")
            : Row(1, "ADB実行ファイル", StateFailed, $"ファイルが存在しません: {path}",
                "プリセットの適用、自動検出、または参照で正しいadb.exeを指定してください。");
    }

    private static SukiAdbDiagnosticStepRow BuildAdbLaunchStep(
        RhodesAdbDiagnosticsInput input,
        SukiAdbDiagnosticStepRow adbPathStep)
    {
        if (!input.DetectionAttempted)
        {
            return Row(2, "ADB起動", StateUnknown, "まだ確認していません。",
                "「自動検出」または「診断実行」を押してください。");
        }

        if (input.HasAvailableAdb)
            return Row(2, "ADB起動", StateOk, "adbの実行を確認しました。", "");

        return adbPathStep.State == StateFailed
            ? Row(2, "ADB起動", StateFailed, "実行可能なADBがありません。", "手順1を先に解決してください。")
            : Row(2, "ADB起動", StateFailed, "adbを起動できませんでした。",
                "VC++再頒布パッケージ不足、権限、ウイルス対策の隔離を確認してください。");
    }

    private static SukiAdbDiagnosticStepRow BuildDeviceStep(
        RhodesAdbDiagnosticsInput input,
        SukiAdbDiagnosticStepRow adbLaunchStep)
    {
        if (!input.DetectionAttempted)
            return Row(3, "端末検出", StateUnknown, "まだ確認していません。", "「自動検出」を押してください。");

        if (adbLaunchStep.State == StateFailed)
            return Row(3, "端末検出", StateUnknown, "ADBが起動できないため確認できません。", "手順2を先に解決してください。");

        if (input.DeviceCount == 0)
        {
            return Row(3, "端末検出", StateFailed, "端末が見つかりません。",
                "エミュレータを起動し、adb connect対象のポート(例 127.0.0.1:16384)を確認してください。");
        }

        if (input.UsableDeviceCount == 0)
        {
            return Row(3, "端末検出", StateWarning, $"端末はありますが使用できません: {input.DeviceSummary}",
                "offline/unauthorizedの場合はエミュレータ再起動、USBデバッグ許可を確認してください。");
        }

        return input.DeviceCount > 1
            ? Row(3, "端末検出", StateWarning, $"複数端末: {input.DeviceSummary}",
                "使用する端末の「使用」を押してserialを固定してください。")
            : Row(3, "端末検出", StateOk, input.DeviceSummary, "");
    }

    private static SukiAdbDiagnosticStepRow BuildMaaStep(RhodesAdbDiagnosticsInput input)
    {
        return input.MaaReady switch
        {
            null => Row(4, "MAA接続", StateUnknown, "まだ確認していません。", "「診断実行」または「接続」を押してください。"),
            true => Row(4, "MAA接続", StateOk, input.SessionDetail, ""),
            false => Row(4, "MAA接続", StateFailed, input.SessionDetail,
                "DLL not found / VC++不足の場合はMAAFramework runtimeを確認してください。"),
        };
    }

    private static SukiAdbDiagnosticStepRow BuildCaptureStep(RhodesAdbDiagnosticsInput input)
    {
        return input.CaptureSucceeded switch
        {
            null => Row(5, "スクショ取得", StateUnknown, "まだ確認していません。", "「診断実行」または「撮影」を押してください。"),
            true => Row(5, "スクショ取得", StateOk, input.CaptureDetail, ""),
            false => Row(5, "スクショ取得", StateFailed, input.CaptureDetail,
                "撮影方式を標準または互換方式に変更して再試行してください。"),
        };
    }

    private static SukiAdbDiagnosticStepRow BuildResolutionStep(RhodesAdbDiagnosticsInput input)
    {
        if (input.PixelWidth <= 0 || input.PixelHeight <= 0)
        {
            return Row(6, "解像度 1280x720 / 16:9", StateUnknown, "スクショ未取得のため確認できません。",
                "手順5を先に完了してください。");
        }

        var size = $"{input.PixelWidth}x{input.PixelHeight}";
        if (input.PixelWidth == 1280 && input.PixelHeight == 720)
            return Row(6, "解像度 1280x720 / 16:9", StateOk, size, "");

        var is16To9 = input.PixelWidth * 9 == input.PixelHeight * 16;
        return is16To9
            ? Row(6, "解像度 1280x720 / 16:9", StateWarning, $"{size} (16:9)",
                "認識精度の基準は1280x720です。エミュレータ解像度の変更を推奨します。")
            : Row(6, "解像度 1280x720 / 16:9", StateFailed, $"{size} (16:9ではありません)",
                "エミュレータの解像度を1280x720に設定してください。");
    }

    private static SukiAdbDiagnosticStepRow Row(int order, string label, string state, string detail, string nextAction)
    {
        var (foreground, background, border) = state switch
        {
            StateOk => ("#7BE2B6", "#12241D", "#2C5745"),
            StateWarning => ("#F0D06A", "#242012", "#5C512C"),
            StateFailed => ("#F08A8A", "#241212", "#5C2C2C"),
            _ => ("#AAB6B8", "#151D1E", "#2B3638"),
        };
        return new SukiAdbDiagnosticStepRow(
            order,
            $"{order}. {label}",
            state,
            string.IsNullOrWhiteSpace(detail) ? "-" : detail.Trim(),
            nextAction.Trim(),
            !string.IsNullOrWhiteSpace(nextAction),
            foreground,
            background,
            border);
    }
}

public sealed record RhodesAdbDiagnosticsInput(
    string? AdbPath,
    bool DetectionAttempted,
    bool HasAvailableAdb,
    int DeviceCount,
    int UsableDeviceCount,
    string DeviceSummary,
    bool? MaaReady,
    string SessionDetail,
    bool? CaptureSucceeded,
    string CaptureDetail,
    int PixelWidth,
    int PixelHeight);

public sealed record RhodesAdbDiagnosticsCopyInput(
    string Campaign,
    string AdbPreset,
    string AdbPath,
    string AdbSerial,
    string ScreencapMethod,
    string ScreencapMethodId,
    string InputMethod,
    string InputMethodId,
    string OcrEngine,
    string OcrEngineId,
    string CaptureSize,
    string CaptureDetail,
    string MaaState,
    string SessionDetail,
    string DetectionSummary,
    string DetectionDetail);

public sealed record SukiAdbDiagnosticStepRow(
    int Order,
    string Label,
    string State,
    string Detail,
    string NextAction,
    bool HasNextAction,
    string StateForeground,
    string StateBackground,
    string StateBorder);
