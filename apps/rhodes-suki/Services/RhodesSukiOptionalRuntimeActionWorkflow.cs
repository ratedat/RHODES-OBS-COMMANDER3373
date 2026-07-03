using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesSukiOptionalRuntimeActionSnapshot(
    SukiOptionalRuntimeStatus RuntimeStatus,
    SukiOptionalRuntimeStatus ApiStatus,
    string StatusMessage);

public static class RhodesSukiOptionalRuntimeActionWorkflow
{
    public static async Task<RhodesSukiOptionalRuntimeActionSnapshot> RunAsync(
        string label,
        Func<string, HttpClient?, Task<SukiOptionalRuntimeActionResult>> action,
        string rhodesApiUrl,
        HttpClient? httpClient = null)
    {
        var result = await action(rhodesApiUrl, httpClient);
        return new RhodesSukiOptionalRuntimeActionSnapshot(
            result.Status,
            BuildApiStatus(label, result),
            BuildStatusMessage(label, result));
    }

    private static SukiOptionalRuntimeStatus BuildApiStatus(string label, SukiOptionalRuntimeActionResult result)
    {
        return result.Succeeded
            ? new SukiOptionalRuntimeStatus("RHODES API", "接続済み", $"{label} API実行済み", true, false)
            : new SukiOptionalRuntimeStatus("RHODES API", "接続失敗", result.Error, false, false);
    }

    private static string BuildStatusMessage(string label, SukiOptionalRuntimeActionResult result)
    {
        return result.Succeeded
            ? $"{label}: {result.Status.State}"
            : $"{label}失敗: {result.Error}";
    }
}
