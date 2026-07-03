using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionWorkspaceRegistry
{
    public static SukiRecognitionWorkspaceLayout Layout { get; } = new(
        new SukiWorkspaceSectionPreview(
            "recognition",
            "認識ワークフロー",
            "MAA Resource taskを実行し、候補化してstate/APIへ反映します。"),
        new SukiWorkspaceSectionPreview(
            "profile",
            "プロファイル選択",
            "取得対象とResource task群を選び、16:9 / 1280x720基準の認識単位を切り替えます。"),
        new SukiWorkspaceSectionPreview(
            "execution",
            "実行",
            "MAA Controllerで撮影とResource taskを実行し、候補化APIとローカル補完へ送ります。"),
        new SukiWorkspaceSectionPreview(
            "review",
            "候補確認",
            "候補数、反映結果、読み込み履歴を確認し、必要なら再読み込みします。"),
        new SukiWorkspaceSectionPreview(
            "evidence",
            "検証情報",
            "Resource task結果、OCR/ROI詳細、履歴JSONをデバッグと調整に使います。"));
}
