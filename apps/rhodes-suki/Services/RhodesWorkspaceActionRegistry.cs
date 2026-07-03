using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesWorkspaceActionRegistry
{
    private static readonly SukiWorkspaceActionDescriptor[] Descriptors =
    [
        new(
            "run.sync-state",
            "run",
            "values",
            "API状態同期",
            "SyncRunStateFromApiCommand",
            "state-api.import",
            "RHODES APIの現在stateをSukiローカルへ取り込みます。",
            false,
            true,
            0),
        new(
            "run.recognize-base",
            "run",
            "values",
            "共通値を認識",
            "OpenRecognitionProfileCommand(runStatusFull)",
            "maa-resource.profile",
            "源石錐、等級、分隊、IS固有値だけをMAA Resourceプロファイルへ渡します。",
            true,
            false,
            1),
        new(
            "run.recognize-is5-thought",
            "run",
            "special",
            "思案認識",
            "OpenRecognitionProfileCommand(is5ThoughtFull)",
            "maa-resource.profile",
            "IS#5の思案一覧をMAA Resourceで読み、候補レビューへ送ります。",
            true,
            false,
            2),
        new(
            "choices.recognize-operators",
            "choices",
            "catalog",
            "オペ認識",
            "OpenRecognitionProfileCommand(operatorsFull)",
            "maa-resource.profile",
            "開いている隊員画面からオペレーター候補を生成します。",
            true,
            false,
            10),
        new(
            "choices.recognize-relics",
            "choices",
            "catalog",
            "秘宝認識",
            "OpenRecognitionProfileCommand(relicsFull)",
            "maa-resource.profile",
            "秘宝一覧から所持秘宝候補を生成します。",
            true,
            false,
            11),
        new(
            "choices.clear-visible",
            "choices",
            "selection",
            "表示中の選択解除",
            "ClearVisibleChoicesCommand",
            "local-state.selection",
            "現在のフィルターで表示中の選択だけを解除します。",
            false,
            true,
            12),
        new(
            "recognition.run-profile",
            "recognition",
            "execution",
            "選択プロファイル実行",
            "RunSelectedProfileRecognitionCommand",
            "maa-resource.tasker",
            "MAAFramework Taskerで選択中プロファイルのResource taskを実行し、候補化します。",
            true,
            false,
            20),
        new(
            "recognition.run-and-apply",
            "recognition",
            "execution",
            "認識して反映",
            "RunSelectedProfileRecognitionAndApplyCommand",
            "maa-resource.tasker+state-api.apply",
            "候補化後にAPI優先でstateへ反映し、失敗時はローカルstateへfallbackします。",
            true,
            true,
            21),
        new(
            "recognition.apply-candidates",
            "recognition",
            "review",
            "候補を反映",
            "ApplyCandidateResultsCommand",
            "state-api.apply",
            "現在の候補一覧をレビュー結果としてstateへ反映します。",
            false,
            true,
            22),
        new(
            "recognition.refresh-history",
            "recognition",
            "evidence",
            "履歴更新",
            "RefreshRecognitionScanHistoryCommand",
            "evidence.history",
            "MAA証跡と旧API証跡を同じ履歴ビューへ読み込みます。",
            false,
            false,
            23),
        new(
            "output.open-sidecar",
            "output",
            "preview",
            "Sidecar",
            "OpenPreviewUrlCommand(/sidecar)",
            "browser-preview",
            "OBS隣接確認用のサイドカー表示を開きます。",
            false,
            false,
            40),
        new(
            "output.open-overlay",
            "output",
            "preview",
            "Overlay",
            "OpenPreviewUrlCommand(/overlay)",
            "browser-preview",
            "OBSブラウザソース用のOverlay表示を開きます。",
            false,
            false,
            41),
        new(
            "runtime.save-settings",
            "runtime",
            "connection",
            "保存",
            "SaveSettingsCommand",
            "state-api.settings",
            "ADB、OCR、出力設定をcurrent-stateの既存スキーマへ同期します。",
            false,
            true,
            30),
        new(
            "runtime.auto-detect",
            "runtime",
            "detection",
            "自動検出",
            "RefreshAdbDevicesCommand",
            "adb.local-detect",
            "MAA風のADB候補と接続済み端末を検出します。",
            false,
            false,
            31),
        new(
            "runtime.connection-test",
            "runtime",
            "connection",
            "接続テスト",
            "RunAdbConnectionTestCommand",
            "maa-controller.preflight",
            "MAA Controller接続と撮影可否を確認します。",
            true,
            false,
            32),
        new(
            "runtime.connect",
            "runtime",
            "connection",
            "接続",
            "ConnectCommand",
            "maa-controller.connect",
            "現在のADB path/serial/configでMAA Controllerを初期化します。",
            true,
            false,
            33),
        new(
            "runtime.capture",
            "runtime",
            "connection",
            "撮影",
            "CaptureCommand",
            "maa-controller.cached-screenshot",
            "MAA Controllerからスクリーンショットを取得し、デバッグログへ保存します。",
            true,
            false,
            34),
        new(
            "runtime.probe-status",
            "runtime",
            "diagnostics",
            "状態確認",
            "RefreshOptionalRuntimesCommand",
            "runtime.probe",
            "RHODES API、Master Data、GLM/Ollama、Hyper-Vの状態を集約します。",
            false,
            false,
            35),
        new(
            "debug.roi-regenerate",
            "debug",
            "logs",
            "Resource再生成",
            "RegenerateMaaResourceCommand",
            "maa-resource.generate",
            "ROI調整後の生成元からMAA Resourceを再生成し、必要ならMAAセッションへ再読込します。",
            true,
            true,
            50),
    ];

    private static readonly HashSet<string> SupportedCommandNames = new(StringComparer.Ordinal)
    {
        "SyncRunStateFromApiCommand",
        "OpenRecognitionProfileCommand",
        "ClearVisibleChoicesCommand",
        "RunSelectedProfileRecognitionCommand",
        "RunSelectedProfileRecognitionAndApplyCommand",
        "ApplyCandidateResultsCommand",
        "RefreshRecognitionScanHistoryCommand",
        "OpenPreviewUrlCommand",
        "SaveSettingsCommand",
        "RefreshAdbDevicesCommand",
        "RunAdbConnectionTestCommand",
        "ConnectCommand",
        "CaptureCommand",
        "RefreshOptionalRuntimesCommand",
        "RegenerateMaaResourceCommand",
    };

    public static IReadOnlyList<SukiWorkspaceActionDescriptor> Items => Descriptors;

    public static IReadOnlyList<SukiWorkspaceActionDescriptor> ForWorkspace(string? workspaceId)
    {
        var normalized = RhodesWorkspaceRegistry.Normalize(workspaceId);
        return Descriptors
            .Where(item => string.Equals(item.WorkspaceId, normalized, StringComparison.Ordinal))
            .OrderBy(item => item.DisplayPriority)
            .ToArray();
    }

    public static SukiWorkspaceActionCommandSpec ParseCommandName(string? commandName)
    {
        var trimmed = commandName?.Trim() ?? "";
        var open = trimmed.IndexOf('(', StringComparison.Ordinal);
        if (open < 0)
            return new SukiWorkspaceActionCommandSpec(trimmed, null);

        var close = trimmed.LastIndexOf(')');
        if (close != trimmed.Length - 1 || close <= open)
            return new SukiWorkspaceActionCommandSpec(trimmed, null);

        var name = trimmed[..open].Trim();
        var parameter = trimmed[(open + 1)..close].Trim();
        return new SukiWorkspaceActionCommandSpec(name, parameter);
    }

    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Descriptors)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                errors.Add("workspace action id is blank");
            else if (!ids.Add(item.Id))
                errors.Add($"duplicate workspace action id: {item.Id}");

            var layout = RhodesWorkspaceLayoutRegistry.For(item.WorkspaceId);
            if (!RhodesWorkspaceRegistry.IsKnown(item.WorkspaceId))
                errors.Add($"{item.Id}: unknown workspace {item.WorkspaceId}");
            else if (!layout.Sections.Any(section => string.Equals(section.Id, item.SectionId, StringComparison.Ordinal)))
                errors.Add($"{item.Id}: unknown section {item.WorkspaceId}.{item.SectionId}");

            if (string.IsNullOrWhiteSpace(item.Label))
                errors.Add($"{item.Id}: label is blank");
            if (string.IsNullOrWhiteSpace(item.CommandName))
                errors.Add($"{item.Id}: command name is blank");
            else
            {
                var command = ParseCommandName(item.CommandName);
                if (!SupportedCommandNames.Contains(command.CommandName))
                    errors.Add($"{item.Id}: unsupported command {item.CommandName}");
                if (item.CommandName.Contains('(', StringComparison.Ordinal)
                    && string.IsNullOrWhiteSpace(command.CommandParameter))
                {
                    errors.Add($"{item.Id}: command parameter is blank or malformed");
                }
            }
            if (string.IsNullOrWhiteSpace(item.Workflow))
                errors.Add($"{item.Id}: workflow is blank");
            if (string.IsNullOrWhiteSpace(item.Detail))
                errors.Add($"{item.Id}: detail is blank");
        }

        return errors;
    }
}
