using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesProductSurfaceRegistry
{
    private static readonly SukiProductSurfaceDescriptor[] Descriptors =
    [
        new(
            "run.base",
            "run-field",
            "run",
            "run",
            "catalog/manual/MAA-OCR",
            "run-field",
            "candidate-review",
            true,
            0),
        new(
            "run.special",
            "campaign-special-field",
            "special",
            "run.special",
            "campaign-catalog/MAA-OCR",
            "special-field",
            "candidate-review",
            true,
            1),
        new(
            "choices.operators",
            "choice-catalog",
            "choices",
            "operators",
            "master-data/manual/MAA-OCR/GLM-OCR",
            "choice-item",
            "candidate-review",
            true,
            10),
        new(
            "choices.relics",
            "choice-catalog",
            "choices",
            "relics",
            "master-data/manual/MAA-OCR/GLM-OCR",
            "choice-item",
            "candidate-review",
            true,
            11),
        new(
            "recognition.profiles",
            "recognition-profile",
            "recognition",
            "recognition.profiles",
            "MAAFramework interface/resource",
            "resource-profile",
            "manual-run",
            false,
            20),
        new(
            "recognition.candidates",
            "candidate-review-type",
            "recognition",
            "recognition.candidates",
            "MAA-OCR/template/GLM-OCR",
            "candidate-evidence",
            "apply-or-ignore",
            false,
            21),
        new(
            "recognition.evidence",
            "debug-artifact",
            "recognition",
            "recognition.evidence",
            "MAAFramework task detail/screenshots/logs",
            "evidence-tree",
            "read-only",
            false,
            22),
        new(
            "recognition.roi-adjustment",
            "debug-artifact",
            "recognition",
            "recognition.roiAdjustments",
            "MAAFramework ROI detail/resource generation",
            "roi-editor",
            "manual-confirm",
            false,
            23),
        new(
            "output.obs-parts",
            "output-part",
            "output",
            "preferences.sukiOutputParts",
            "manual/state-sync",
            "output-part",
            "manual",
            true,
            30),
        new(
            "runtime.adb",
            "runtime-capability",
            "runtime",
            "adb",
            "MAAFramework Controller",
            "runtime-command",
            "manual",
            false,
            40),
        new(
            "runtime.maa-framework",
            "runtime-capability",
            "runtime",
            "maa-framework-runtime",
            "Maa.Framework binding/native runtime",
            "runtime-diagnostic",
            "required",
            false,
            41),
        new(
            "runtime.maa-ocr",
            "runtime-capability",
            "runtime",
            "resource.base.model.ocr",
            "MAAFramework Resource",
            "runtime-diagnostic",
            "required",
            false,
            42),
        new(
            "runtime.glm-ocr",
            "runtime-capability",
            "runtime",
            "glm-ocr-runtime",
            "optional-download",
            "runtime-diagnostic",
            "optional",
            false,
            43),
        new(
            "runtime.ollama",
            "runtime-capability",
            "runtime",
            "ollama-runtime",
            "optional-download/local model runtime",
            "runtime-diagnostic",
            "optional",
            false,
            44),
        new(
            "runtime.hyper-v",
            "runtime-capability",
            "runtime",
            "windows.hypervisor",
            "Windows platform diagnostics",
            "runtime-diagnostic",
            "platform-diagnostic",
            false,
            45),
        new(
            "debug.evidence",
            "debug-artifact",
            "debug",
            "debugLogs",
            "MAAFramework evidence/logs",
            "evidence-tree",
            "read-only",
            false,
            50),
        new(
            "debug.logs",
            "debug-artifact",
            "debug",
            "debugLogs.logs",
            "ADB/MAA/API runtime logs",
            "log-list",
            "read-only",
            false,
            51),
        new(
            "debug.roi-sessions",
            "debug-artifact",
            "debug",
            "debugLogs.roiSessions",
            "MAAFramework ROI adjustment sessions",
            "roi-session-list",
            "manual-confirm",
            false,
            52),
    ];

    public static IReadOnlyList<SukiProductSurfaceDescriptor> Items => Descriptors;

    public static IReadOnlyList<SukiProductSurfaceDescriptor> ForWorkspace(string? workspaceId)
    {
        var normalized = RhodesWorkspaceRegistry.Normalize(workspaceId);
        return Descriptors
            .Where(item => string.Equals(item.WorkspaceId, normalized, StringComparison.Ordinal))
            .OrderBy(item => item.DisplayPriority)
            .ToArray();
    }

    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in Descriptors)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                errors.Add("surface id is blank");
            else if (!ids.Add(item.Id))
                errors.Add($"duplicate surface id: {item.Id}");

            if (!RhodesWorkspaceRegistry.IsKnown(item.WorkspaceId))
                errors.Add($"{item.Id}: unknown workspace {item.WorkspaceId}");
            if (string.IsNullOrWhiteSpace(item.Category))
                errors.Add($"{item.Id}: category is blank");
            if (string.IsNullOrWhiteSpace(item.StatePath))
                errors.Add($"{item.Id}: state path is blank");
            if (string.IsNullOrWhiteSpace(item.Provenance))
                errors.Add($"{item.Id}: provenance is blank");
            if (string.IsNullOrWhiteSpace(item.InspectorKind))
                errors.Add($"{item.Id}: inspector kind is blank");
            if (string.IsNullOrWhiteSpace(item.ReviewPolicy))
                errors.Add($"{item.Id}: review policy is blank");
        }
        return errors;
    }
}
