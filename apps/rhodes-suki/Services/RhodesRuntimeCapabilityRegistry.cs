using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRuntimeCapabilityRegistry
{
    public static IReadOnlyList<SukiRuntimeCapabilityPreview> Build(SukiRuntimeCapabilityContext context)
    {
        return
        [
            new SukiRuntimeCapabilityPreview(
                "adb",
                "ADB",
                "CORE",
                context.AdbState,
                context.AdbDetail,
                "端末一覧",
                false),
            new SukiRuntimeCapabilityPreview(
                "maa",
                "MAAFramework",
                "CORE",
                context.MaaFrameworkStatus.State,
                context.MaaFrameworkStatus.Detail,
                "接続",
                false),
            new SukiRuntimeCapabilityPreview(
                "maa-ocr",
                "MAA-OCR",
                "OCR",
                context.MaaOcrState,
                context.MaaOcrDetail,
                "認識",
                false),
            new SukiRuntimeCapabilityPreview(
                "glm",
                "GLM-OCR",
                "OPTIONAL",
                context.GlmStatus.State,
                context.GlmStatus.Detail,
                "状態確認",
                !context.GlmStatus.Installed),
            new SukiRuntimeCapabilityPreview(
                "ollama",
                "Ollama",
                "OPTIONAL",
                context.OllamaStatus.State,
                context.OllamaStatus.Detail,
                "状態確認",
                !context.OllamaStatus.Installed),
            new SukiRuntimeCapabilityPreview(
                "hyperv",
                "Hyper-V",
                "PLATFORM",
                context.HypervisorStatus.State,
                context.HypervisorStatus.Detail,
                "診断",
                false)
        ];
    }
}
