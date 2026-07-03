using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesSukiStateSyncRequest(
    IReadOnlyList<SukiChoiceItem> Operators,
    IReadOnlyList<SukiChoiceItem> Relics,
    SukiChoicePersistenceOptions ChoiceOptions,
    RhodesAdbApiSettings AdbSettings,
    SukiOutputPreferences OutputPreferences,
    string OcrEngine);

public sealed record RhodesSukiStateSyncResult(
    string Error,
    SukiOptionalRuntimeStatus ApiStatus,
    bool LocalStateReplaced,
    string StatusMessage)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);

    public bool ShouldReloadRunState => Succeeded && LocalStateReplaced;
}

public static class RhodesSukiStateSyncWorkflow
{
    public static async Task<RhodesSukiStateSyncResult> SyncSettingsAsync(
        RhodesSukiStateSyncRequest request,
        Func<CancellationToken, Task<RhodesStateApiResult>> fetchApiStateAsync,
        Func<string, CancellationToken, Task<RhodesStateApiResult>> saveApiStateAsync,
        Func<string, CancellationToken, Task> replaceLocalStateJsonAsync,
        CancellationToken cancellationToken = default)
    {
        var fetched = await fetchApiStateAsync(cancellationToken);
        if (!fetched.Succeeded)
            return Failure(fetched.Error, "ADB API設定");

        var updated = RhodesStateApiClient.ApplyChoicesToStateJson(
            fetched.StateJson,
            request.Operators,
            request.Relics,
            request.ChoiceOptions);
        updated = RhodesStateApiClient.ApplyAdbSettingsToStateJson(updated, request.AdbSettings);
        updated = RhodesStateApiClient.ApplySukiPreferencesToStateJson(
            updated,
            request.ChoiceOptions,
            request.OutputPreferences,
            request.OcrEngine);

        var saved = await saveApiStateAsync(updated, cancellationToken);
        if (!saved.Succeeded)
            return Failure(saved.Error, "ADB API設定");

        await replaceLocalStateJsonAsync(saved.StateJson, cancellationToken);
        return new RhodesSukiStateSyncResult(
            "",
            RhodesApiStatusProbe.ParseStateJson(saved.StateJson),
            true,
            "Suki設定とADB API設定を同期しました。");
    }

    public static async Task<RhodesSukiStateSyncResult> SyncRunContextAsync(
        string campaignId,
        Func<CancellationToken, Task<RhodesStateApiResult>> fetchApiStateAsync,
        Func<string, CancellationToken, Task<RhodesStateApiResult>> saveApiStateAsync,
        Func<string, CancellationToken, Task> replaceLocalStateJsonAsync,
        CancellationToken cancellationToken = default)
    {
        var fetched = await fetchApiStateAsync(cancellationToken);
        if (!fetched.Succeeded)
            return Failure(fetched.Error, "IS切替");

        var updated = RhodesStateApiClient.ApplyRunContextToStateJson(fetched.StateJson, campaignId);
        var saved = await saveApiStateAsync(updated, cancellationToken);
        if (!saved.Succeeded)
            return Failure(saved.Error, "IS切替");

        await replaceLocalStateJsonAsync(saved.StateJson, cancellationToken);
        return new RhodesSukiStateSyncResult(
            "",
            RhodesApiStatusProbe.ParseStateJson(saved.StateJson),
            true,
            "現在ISをAPIへ同期しました。");
    }

    public static async Task<RhodesSukiStateSyncResult> SyncFromApiAsync(
        Func<CancellationToken, Task<RhodesStateApiResult>> fetchApiStateAsync,
        Func<string, CancellationToken, Task> replaceLocalStateJsonAsync,
        CancellationToken cancellationToken = default)
    {
        var fetched = await fetchApiStateAsync(cancellationToken);
        if (!fetched.Succeeded)
            return Failure(fetched.Error, "API state取り込み");

        await replaceLocalStateJsonAsync(fetched.StateJson, cancellationToken);
        return new RhodesSukiStateSyncResult(
            "",
            RhodesApiStatusProbe.ParseStateJson(fetched.StateJson),
            true,
            "API stateをローカルへ取り込みました。");
    }

    private static RhodesSukiStateSyncResult Failure(string error, string label)
    {
        return new RhodesSukiStateSyncResult(
            error,
            new SukiOptionalRuntimeStatus("RHODES API", "接続失敗", error, false, false),
            false,
            $"{label}の同期に失敗しました: {error}");
    }
}
