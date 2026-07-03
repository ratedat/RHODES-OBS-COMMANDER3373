using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRuntimeWorkspaceRegistry
{
    public static SukiRuntimeWorkspaceLayout Layout { get; } = new(
        new SukiRuntimeSectionPreview(
            "runtime",
            "ランタイム",
            "MAA ADB接続、MuMu高速撮影、MAA-OCR、任意GLM/Ollamaをここで管理します。"),
        new SukiRuntimeSectionPreview(
            "connection",
            "接続設定",
            "ADBプリセット、実行ファイル、serial、MAAの撮影方式と入力方式を設定します。"),
        new SukiRuntimeSectionPreview(
            "detection",
            "検出結果",
            "自動検出したADB候補と接続済み端末から、実際に使う対象を選びます。"),
        new SukiRuntimeSectionPreview(
            "diagnostics",
            "診断",
            "MAAFramework、MAA-OCR、GLM/Ollama、Hyper-Vなどの稼働状態を確認します。"),
        new SukiRuntimeSectionPreview(
            "optional",
            "任意OCR",
            "GLM-OCR/Ollamaは必要な検証環境だけで導入し、一般向け既定にはしません。"));
}
