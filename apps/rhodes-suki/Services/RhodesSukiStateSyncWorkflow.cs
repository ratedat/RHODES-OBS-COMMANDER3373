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
    string StatusMessage,
    RhodesStateApiFailureKind FailureKind = RhodesStateApiFailureKind.None)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);

    public bool ShouldReloadRunState => Succeeded && LocalStateReplaced;

    public bool IsUnavailable => !Succeeded && FailureKind == RhodesStateApiFailureKind.Unavailable;
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
            return Failure(fetched, "設定保存");

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
            return Failure(saved, "設定保存");

        await replaceLocalStateJsonAsync(saved.StateJson, cancellationToken);
        return new RhodesSukiStateSyncResult(
            "",
            RhodesApiStatusProbe.ParseStateJson(saved.StateJson),
            true,
            "Suki設定を配信画面へ同期しました。");
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
            return Failure(fetched, "IS切替");

        var updated = RhodesStateApiClient.ApplyRunContextToStateJson(fetched.StateJson, campaignId);
        var saved = await saveApiStateAsync(updated, cancellationToken);
        if (!saved.Succeeded)
            return Failure(saved, "IS切替");

        await replaceLocalStateJsonAsync(saved.StateJson, cancellationToken);
        return new RhodesSukiStateSyncResult(
            "",
            RhodesApiStatusProbe.ParseStateJson(saved.StateJson),
            true,
            "現在ISを配信画面へ同期しました。");
    }

    public static async Task<RhodesSukiStateSyncResult> SyncFromApiAsync(
        Func<CancellationToken, Task<RhodesStateApiResult>> fetchApiStateAsync,
        Func<string, CancellationToken, Task> replaceLocalStateJsonAsync,
        CancellationToken cancellationToken = default)
    {
        var fetched = await fetchApiStateAsync(cancellationToken);
        if (!fetched.Succeeded)
            return Failure(fetched, "状態取り込み");

        await replaceLocalStateJsonAsync(fetched.StateJson, cancellationToken);
        return new RhodesSukiStateSyncResult(
            "",
            RhodesApiStatusProbe.ParseStateJson(fetched.StateJson),
            true,
            "配信画面の状態をローカルへ取り込みました。");
    }

    private static RhodesSukiStateSyncResult Failure(RhodesStateApiResult failure, string label)
    {
        var isUnavailable = failure.FailureKind == RhodesStateApiFailureKind.Unavailable;
        return new RhodesSukiStateSyncResult(
            failure.Error,
            new SukiOptionalRuntimeStatus(
                "配信サーバー",
                isUnavailable ? "未起動" : "接続失敗",
                failure.Error,
                false,
                false),
            false,
            isUnavailable
                ? $"{label}: 配信サーバーは未起動です。"
                : $"{label}: 配信画面への反映に失敗しました。",
            failure.FailureKind);
    }
}
