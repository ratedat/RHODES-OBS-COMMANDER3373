using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesWorkspaceLayoutRegistry
{
    private static readonly SukiWorkspaceLayout[] Layouts =
    [
        new(
            "run",
            new SukiWorkspaceSectionPreview(
                "run",
                "ラン取得値",
                "源石錐・等級・分隊とIS固有値をここでレビューします。"),
            [
                new SukiWorkspaceSectionPreview("values", "共通取得値", "源石錐、等級、分隊など、全ISで保持する取得値です。"),
                new SukiWorkspaceSectionPreview("special", "IS固有値", "思案、啓示、灯火など、現在ISだけが持つ値です。"),
                new SukiWorkspaceSectionPreview("campaign", "IS切替", "現在ランの統合戦略とキャンペーン固有フィールドを切り替えます。"),
            ]),
        new(
            "choices",
            new SukiWorkspaceSectionPreview(
                "choices",
                "選択カタログ",
                "オペレーターと秘宝を検索、フィルター、選択します。"),
            [
                new SukiWorkspaceSectionPreview("catalog", "カタログ", "オペレーターと秘宝の選択対象を切り替えます。"),
                new SukiWorkspaceSectionPreview("filters", "フィルター", "検索、レアリティ、職業、職分、カテゴリ、選択状態で絞り込みます。"),
                new SukiWorkspaceSectionPreview("selection", "選択状態", "直接クリックで選択し、OBS表示へ同期する対象を管理します。"),
            ]),
        new(
            "recognition",
            new SukiWorkspaceSectionPreview(
                "recognition",
                "認識ワークフロー",
                "MAA Resource taskを実行し、候補化してstate/APIへ反映します。"),
            [
                new SukiWorkspaceSectionPreview("profile", "プロファイル選択", "取得対象とResource task群を選び、16:9 / 1280x720基準の認識単位を切り替えます。"),
                new SukiWorkspaceSectionPreview("execution", "実行", "MAA Controllerで撮影とResource taskを実行し、候補化APIとローカル補完へ送ります。"),
                new SukiWorkspaceSectionPreview("review", "候補確認", "候補数、反映結果、読み込み履歴を確認し、必要なら再読み込みします。"),
                new SukiWorkspaceSectionPreview("evidence", "検証情報", "Resource task結果、OCR/ROI詳細、履歴JSONをデバッグと調整に使います。"),
            ]),
        new(
            "output",
            new SukiWorkspaceSectionPreview(
                "output",
                "出力 / OBS",
                "サイドカーや大会向け表示で使う表示部品をここへ集約します。"),
            [
                new SukiWorkspaceSectionPreview("parts", "表示部品", "OBSプレビュー、ブラウザソース、サイドカー表示で共通の表示部品を管理します。"),
                new SukiWorkspaceSectionPreview("preview", "プレビュー", "Sidecar/Overlayの表示先を開き、表示設定の反映を確認します。"),
            ]),
        new(
            "runtime",
            new SukiWorkspaceSectionPreview(
                "runtime",
                "ランタイム",
                "MAA ADB接続、MuMu高速撮影、MAA-OCR、任意GLM/Ollamaをここで管理します。"),
            [
                new SukiWorkspaceSectionPreview("connection", "接続設定", "ADBプリセット、実行ファイル、serial、MAAの撮影方式と入力方式を設定します。"),
                new SukiWorkspaceSectionPreview("detection", "検出結果", "自動検出したADB候補と接続済み端末から、実際に使う対象を選びます。"),
                new SukiWorkspaceSectionPreview("diagnostics", "診断", "MAAFramework、MAA-OCR、GLM/Ollama、Hyper-Vなどの稼働状態を確認します。"),
                new SukiWorkspaceSectionPreview("optional", "任意OCR", "GLM-OCR/Ollamaは必要な検証環境だけで導入し、一般向け既定にはしません。"),
            ]),
        new(
            "debug",
            new SukiWorkspaceSectionPreview(
                "debug",
                "デバッグ",
                "検証用ログ、スクリーンショット、Resource task結果を確認します。"),
            [
                new SukiWorkspaceSectionPreview("logs", "ログ", "ADB撮影、MAA task、候補化、状態同期のログを追跡します。"),
                new SukiWorkspaceSectionPreview("migration", "移行メモ", "MAAFramework/Avalonia本線化の残作業と方針を確認します。"),
            ]),
    ];

    public static IReadOnlyList<SukiWorkspaceLayout> Items => Layouts;

    public static SukiRuntimeWorkspaceLayout Runtime
    {
        get
        {
            var layout = For("runtime");
            return new SukiRuntimeWorkspaceLayout(
                layout.Header,
                Section(layout, "connection"),
                Section(layout, "detection"),
                Section(layout, "diagnostics"),
                Section(layout, "optional"));
        }
    }

    public static SukiRecognitionWorkspaceLayout Recognition
    {
        get
        {
            var layout = For("recognition");
            return new SukiRecognitionWorkspaceLayout(
                layout.Header,
                Section(layout, "profile"),
                Section(layout, "execution"),
                Section(layout, "review"),
                Section(layout, "evidence"));
        }
    }

    public static SukiWorkspaceLayout For(string? workspaceId)
    {
        var normalized = RhodesWorkspaceRegistry.Normalize(workspaceId);
        return Layouts.FirstOrDefault(item => string.Equals(item.WorkspaceId, normalized, StringComparison.Ordinal))
            ?? Layouts[0];
    }

    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var layout in Layouts)
        {
            if (!ids.Add(layout.WorkspaceId))
                errors.Add($"duplicate workspace layout: {layout.WorkspaceId}");
            if (!RhodesWorkspaceRegistry.IsKnown(layout.WorkspaceId))
                errors.Add($"unknown workspace layout: {layout.WorkspaceId}");
            if (string.IsNullOrWhiteSpace(layout.Header.Title))
                errors.Add($"{layout.WorkspaceId}: header title is blank");

            var sectionIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var section in layout.Sections)
            {
                if (string.IsNullOrWhiteSpace(section.Id))
                    errors.Add($"{layout.WorkspaceId}: section id is blank");
                else if (!sectionIds.Add(section.Id))
                    errors.Add($"{layout.WorkspaceId}: duplicate section id {section.Id}");
                if (string.IsNullOrWhiteSpace(section.Title))
                    errors.Add($"{layout.WorkspaceId}.{section.Id}: section title is blank");
                if (string.IsNullOrWhiteSpace(section.Detail))
                    errors.Add($"{layout.WorkspaceId}.{section.Id}: section detail is blank");
            }
        }

        return errors;
    }

    private static SukiWorkspaceSectionPreview Section(SukiWorkspaceLayout layout, string sectionId)
    {
        return layout.Sections.First(section => string.Equals(section.Id, sectionId, StringComparison.Ordinal));
    }
}
