using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesRecognitionTaskExecutionResult(
    MaaResourceExecutionPlan Plan,
    IReadOnlyList<MaaTaskRunResult> TaskResults,
    string Error)
{
    public bool Succeeded => string.IsNullOrWhiteSpace(Error);

    public string Summary => Succeeded
        ? $"{Plan.ProfileLabel} / tasks={TaskResults.Count} / {Plan.Source}"
        : Error;
}

public sealed record RhodesRecognitionCandidateConversionResult(
    IReadOnlyList<MaaCandidatePreview> Candidates,
    string Source,
    string StatusMessage,
    int ApiCandidateCount,
    int LocalCandidateCount,
    int PreviewCandidateCount,
    int SupplementalCandidateCount,
    string ApiError)
{
    public bool HasCandidates => Candidates.Count > 0;
}

public sealed record RhodesCandidateApplyWorkflowResult(
    SukiCandidateApplySummary Summary,
    string ApiError,
    bool LocalFallbackUsed,
    SukiOptionalRuntimeStatus? ApiStatus,
    string LastCandidateApplySummary,
    string StatusMessage,
    string StateJson = "")
{
    public bool ShouldReloadRunState => Summary.AppliedCount > 0;
}

public static class RhodesRecognitionWorkflow
{
    public static async Task<RhodesRecognitionTaskExecutionResult> RunResourceTasksAsync(
        MaaResourceExecutionPlan plan,
        Func<string, CancellationToken, Task<MaaTaskRunResult>> runTaskAsync,
        Action<MaaTaskRunResult>? onTaskResult = null,
        CancellationToken cancellationToken = default)
    {
        if (!plan.CanRun)
            return new RhodesRecognitionTaskExecutionResult(plan, [], plan.Summary);

        var results = new List<MaaTaskRunResult>(plan.Tasks.Count);
        foreach (var task in plan.Tasks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await runTaskAsync(task.Entry, cancellationToken);
            results.Add(result);
            onTaskResult?.Invoke(result);
        }

        return new RhodesRecognitionTaskExecutionResult(plan, results, "");
    }

    public static RhodesRecognitionCandidateConversionResult ConvertCandidates(
        string? profileId,
        IEnumerable<MaaTaskRunResult> taskResults,
        RhodesMaaCandidateApiResult apiResult,
        string? activeCampaignId = null,
        bool apiAttempted = true)
    {
        var results = taskResults as IReadOnlyList<MaaTaskRunResult> ?? taskResults.ToArray();
        if (results.Count == 0)
        {
            return new RhodesRecognitionCandidateConversionResult(
                [],
                "empty",
                "先にResource taskを実行してください。",
                apiResult.Candidates.Count,
                0,
                0,
                0,
                apiResult.Error);
        }

        var localCandidates = RhodesMaaLocalCandidateConverter.FromTaskResults(profileId, results, activeCampaignId);
        if (apiResult.HasCandidates)
        {
            var merged = RhodesMaaCandidateMerger.Merge(apiResult.Candidates, localCandidates);
            var supplementalCount = Math.Max(0, merged.Count - apiResult.Candidates.Count);
            return new RhodesRecognitionCandidateConversionResult(
                merged,
                supplementalCount > 0 ? "api+local" : "api",
                supplementalCount > 0
                    ? $"候補化しました: {merged.Count}件 (ローカル補完 +{supplementalCount})"
                    : $"候補化しました: {merged.Count}件",
                apiResult.Candidates.Count,
                localCandidates.Count,
                0,
                supplementalCount,
                apiResult.Error);
        }

        if (localCandidates.Count > 0)
        {
            return new RhodesRecognitionCandidateConversionResult(
                localCandidates,
                "local",
                string.IsNullOrWhiteSpace(apiResult.Error)
                    ? $"ローカル候補化しました: {localCandidates.Count}件"
                    : $"候補化APIに接続できないためローカル候補化しました: {localCandidates.Count}件",
                apiResult.Candidates.Count,
                localCandidates.Count,
                0,
                localCandidates.Count,
                apiResult.Error);
        }

        var previewCandidates = RhodesMaaResultPreview.FromTaskResults(results);
        if (previewCandidates.Count > 0)
        {
            return new RhodesRecognitionCandidateConversionResult(
                previewCandidates,
                "preview",
                !apiAttempted
                    ? $"ローカルMAAプレビューを表示しました: {previewCandidates.Count}件"
                    : string.IsNullOrWhiteSpace(apiResult.Error)
                    ? $"候補化APIは0件だったためローカルMAAプレビューを表示しました: {previewCandidates.Count}件"
                    : $"候補化APIに接続できないためローカルMAAプレビューを表示しました: {previewCandidates.Count}件",
                apiResult.Candidates.Count,
                localCandidates.Count,
                previewCandidates.Count,
                previewCandidates.Count,
                apiResult.Error);
        }

        return new RhodesRecognitionCandidateConversionResult(
            [],
            "empty",
            !apiAttempted
                ? "ローカル候補は0件です。"
                : string.IsNullOrWhiteSpace(apiResult.Error)
                ? "候補は0件です。"
                : $"候補化API失敗: {apiResult.Error}",
            apiResult.Candidates.Count,
            localCandidates.Count,
            0,
            0,
            apiResult.Error);
    }

    public static async Task<RhodesRecognitionCandidateConversionResult> ConvertCandidatesLocalFirstAsync(
        string? profileId,
        IReadOnlyList<MaaTaskRunResult> taskResults,
        Func<Task<RhodesMaaCandidateApiResult>> convertApiAsync,
        string? activeCampaignId = null,
        bool apiAvailable = true)
    {
        var localCandidateCount = RhodesMaaLocalCandidateConverter.FromTaskResults(
                profileId,
                taskResults,
                activeCampaignId)
            .Count;
        var apiAttempted = apiAvailable && localCandidateCount == 0;
        var apiResult = apiAttempted
            ? await convertApiAsync()
            : new RhodesMaaCandidateApiResult([], "");
        return ConvertCandidates(
            profileId,
            taskResults,
            apiResult,
            activeCampaignId,
            apiAttempted);
    }

    public static async Task<RhodesCandidateApplyWorkflowResult> ApplyCandidatesAsync(
        IReadOnlyList<MaaCandidatePreview> candidates,
        Func<CancellationToken, Task<RhodesStateApiResult>> fetchApiStateAsync,
        Func<string, CancellationToken, Task<RhodesStateApiResult>> saveApiStateAsync,
        Func<string, CancellationToken, Task> replaceLocalStateJsonAsync,
        Func<IReadOnlyList<MaaCandidatePreview>, RhodesCandidateApplyOptions, CancellationToken, Task<SukiCandidateApplySummary>> saveLocalCandidatesAsync,
        CancellationToken cancellationToken = default,
        bool apiAvailable = true,
        RhodesCandidateApplyOptions? applyOptions = null)
    {
        var resolvedApplyOptions = applyOptions ?? RhodesCandidateApplyOptions.Default;
        if (candidates.Count == 0)
        {
            return new RhodesCandidateApplyWorkflowResult(
                SukiCandidateApplySummary.Empty,
                "",
                false,
                null,
                "反映なし: 候補0件",
                "反映する候補がありません。");
        }

        if (!apiAvailable)
        {
            return await ApplyLocalFallbackAsync(
                candidates,
                new RhodesStateApiResult(
                    "",
                    "配信サーバーは未起動です。",
                    RhodesStateApiFailureKind.Unavailable),
                saveLocalCandidatesAsync,
                resolvedApplyOptions,
                cancellationToken);
        }

        var fetched = await fetchApiStateAsync(cancellationToken);
        if (!fetched.Succeeded)
            return await ApplyLocalFallbackAsync(
                candidates,
                fetched,
                saveLocalCandidatesAsync,
                resolvedApplyOptions,
                cancellationToken);

        var applied = RhodesStateApiClient.ApplyCandidatesToStateJson(
            fetched.StateJson,
            candidates,
            applyOptions: resolvedApplyOptions);
        if (applied.Summary.AppliedCount <= 0)
            return NotAppliedResult(applied.Summary, "");

        var saved = await saveApiStateAsync(applied.StateJson, cancellationToken);
        if (!saved.Succeeded)
            return await ApplyLocalFallbackAsync(
                candidates,
                saved,
                saveLocalCandidatesAsync,
                resolvedApplyOptions,
                cancellationToken);

        await replaceLocalStateJsonAsync(saved.StateJson, cancellationToken);
        return AppliedResult(
            applied.Summary,
            "",
            false,
            RhodesApiStatusProbe.ParseStateJson(saved.StateJson),
            saved.StateJson);
    }

    private static async Task<RhodesCandidateApplyWorkflowResult> ApplyLocalFallbackAsync(
        IReadOnlyList<MaaCandidatePreview> candidates,
        RhodesStateApiResult apiFailure,
        Func<IReadOnlyList<MaaCandidatePreview>, RhodesCandidateApplyOptions, CancellationToken, Task<SukiCandidateApplySummary>> saveLocalCandidatesAsync,
        RhodesCandidateApplyOptions applyOptions,
        CancellationToken cancellationToken)
    {
        var summary = await saveLocalCandidatesAsync(candidates, applyOptions, cancellationToken);
        var apiState = apiFailure.IsUnavailable ? "未起動" : "同期失敗";
        var apiStatus = new SukiOptionalRuntimeStatus("配信サーバー", apiState, apiFailure.Error, false, false);
        return summary.AppliedCount <= 0
            ? NotAppliedResult(summary, apiFailure.Error, apiStatus, localFallbackUsed: true, failureKind: apiFailure.FailureKind)
            : AppliedResult(summary, apiFailure.Error, localFallbackUsed: true, apiStatus, failureKind: apiFailure.FailureKind);
    }

    private static RhodesCandidateApplyWorkflowResult AppliedResult(
        SukiCandidateApplySummary summary,
        string apiError,
        bool localFallbackUsed,
        SukiOptionalRuntimeStatus? apiStatus,
        string stateJson = "",
        RhodesStateApiFailureKind failureKind = RhodesStateApiFailureKind.None)
    {
        var fields = BuildAppliedFieldsSummary(summary.AppliedFields);
        return new RhodesCandidateApplyWorkflowResult(
            summary,
            apiError,
            localFallbackUsed,
            apiStatus,
            $"{summary.AppliedCount}件: {fields}",
            string.IsNullOrWhiteSpace(apiError)
                ? $"状態へ反映し、配信画面へ同期しました: {summary.AppliedCount}件 ({fields})"
                : failureKind == RhodesStateApiFailureKind.Unavailable
                    ? $"ローカル状態へ反映しました: {summary.AppliedCount}件 ({fields}) / 配信サーバーは未起動です。OBS連携時に出力画面から起動してください。"
                    : $"ローカル状態へ反映しました: {summary.AppliedCount}件 ({fields}) / 配信画面への反映に失敗しました。",
            stateJson);
    }

    private static string BuildAppliedFieldsSummary(IReadOnlyList<string> appliedFields)
    {
        if (appliedFields.Count <= 3 && appliedFields.All(field => !field.Contains(':')))
            return string.Join(", ", appliedFields);

        return string.Join(
            " / ",
            appliedFields
                .GroupBy(AppliedFieldLabel, StringComparer.Ordinal)
                .Select(group => $"{group.Key}{group.Count()}件"));
    }

    private static string AppliedFieldLabel(string field)
    {
        if (field.Contains(":rejectionReaction:", StringComparison.Ordinal))
            return "拒絶反応";
        if (field.Contains(":operatorEvolution:", StringComparison.Ordinal))
            return "進化";
        if (field.StartsWith("operator:", StringComparison.Ordinal))
            return "オペレーター";
        if (field.StartsWith("relic:", StringComparison.Ordinal))
            return "秘宝";
        if (field.Equals("ingot", StringComparison.Ordinal))
            return "源石錐";
        if (field.Equals("difficulty", StringComparison.Ordinal))
            return "等級";
        if (field.StartsWith("squad", StringComparison.Ordinal))
            return "分隊";
        return "その他";
    }

    private static RhodesCandidateApplyWorkflowResult NotAppliedResult(
        SukiCandidateApplySummary summary,
        string apiError,
        SukiOptionalRuntimeStatus? apiStatus = null,
        bool localFallbackUsed = false,
        RhodesStateApiFailureKind failureKind = RhodesStateApiFailureKind.None)
    {
        return new RhodesCandidateApplyWorkflowResult(
            summary,
            apiError,
            localFallbackUsed,
            apiStatus,
            $"反映なし: 無視 {summary.IgnoredCount}件",
            string.IsNullOrWhiteSpace(apiError)
                ? $"状態へ反映できる候補はありませんでした。無視: {summary.IgnoredCount}件"
                : failureKind == RhodesStateApiFailureKind.Unavailable
                    ? $"状態へ反映できる候補はありませんでした。配信サーバーは未起動です。無視: {summary.IgnoredCount}件"
                    : $"状態へ反映できる候補はありませんでした。配信画面への反映に失敗しました。無視: {summary.IgnoredCount}件");
    }
}
