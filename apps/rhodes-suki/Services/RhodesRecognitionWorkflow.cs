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
    string StatusMessage)
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
        string? activeCampaignId = null)
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
                string.IsNullOrWhiteSpace(apiResult.Error)
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
            string.IsNullOrWhiteSpace(apiResult.Error)
                ? "候補は0件です。"
                : $"候補化API失敗: {apiResult.Error}",
            apiResult.Candidates.Count,
            localCandidates.Count,
            0,
            0,
            apiResult.Error);
    }

    public static async Task<RhodesCandidateApplyWorkflowResult> ApplyCandidatesAsync(
        IReadOnlyList<MaaCandidatePreview> candidates,
        Func<CancellationToken, Task<RhodesStateApiResult>> fetchApiStateAsync,
        Func<string, CancellationToken, Task<RhodesStateApiResult>> saveApiStateAsync,
        Func<string, CancellationToken, Task> replaceLocalStateJsonAsync,
        Func<IReadOnlyList<MaaCandidatePreview>, CancellationToken, Task<SukiCandidateApplySummary>> saveLocalCandidatesAsync,
        CancellationToken cancellationToken = default)
    {
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

        var fetched = await fetchApiStateAsync(cancellationToken);
        if (!fetched.Succeeded)
            return await ApplyLocalFallbackAsync(candidates, fetched.Error, saveLocalCandidatesAsync, cancellationToken);

        var applied = RhodesStateApiClient.ApplyCandidatesToStateJson(fetched.StateJson, candidates);
        if (applied.Summary.AppliedCount <= 0)
            return NotAppliedResult(applied.Summary, "");

        var saved = await saveApiStateAsync(applied.StateJson, cancellationToken);
        if (!saved.Succeeded)
            return await ApplyLocalFallbackAsync(candidates, saved.Error, saveLocalCandidatesAsync, cancellationToken);

        await replaceLocalStateJsonAsync(saved.StateJson, cancellationToken);
        return AppliedResult(applied.Summary, "", false, RhodesApiStatusProbe.ParseStateJson(saved.StateJson));
    }

    private static async Task<RhodesCandidateApplyWorkflowResult> ApplyLocalFallbackAsync(
        IReadOnlyList<MaaCandidatePreview> candidates,
        string apiError,
        Func<IReadOnlyList<MaaCandidatePreview>, CancellationToken, Task<SukiCandidateApplySummary>> saveLocalCandidatesAsync,
        CancellationToken cancellationToken)
    {
        var summary = await saveLocalCandidatesAsync(candidates, cancellationToken);
        var apiStatus = new SukiOptionalRuntimeStatus("RHODES API", "接続失敗", apiError, false, false);
        return summary.AppliedCount <= 0
            ? NotAppliedResult(summary, apiError, apiStatus, localFallbackUsed: true)
            : AppliedResult(summary, apiError, localFallbackUsed: true, apiStatus);
    }

    private static RhodesCandidateApplyWorkflowResult AppliedResult(
        SukiCandidateApplySummary summary,
        string apiError,
        bool localFallbackUsed,
        SukiOptionalRuntimeStatus? apiStatus)
    {
        var fields = string.Join(", ", summary.AppliedFields);
        return new RhodesCandidateApplyWorkflowResult(
            summary,
            apiError,
            localFallbackUsed,
            apiStatus,
            $"{summary.AppliedCount}件: {fields}",
            string.IsNullOrWhiteSpace(apiError)
                ? $"状態へ反映し、APIへ同期しました: {summary.AppliedCount}件 ({fields})"
                : $"状態へ反映しました: {summary.AppliedCount}件 ({fields}) / API同期失敗: {apiError}");
    }

    private static RhodesCandidateApplyWorkflowResult NotAppliedResult(
        SukiCandidateApplySummary summary,
        string apiError,
        SukiOptionalRuntimeStatus? apiStatus = null,
        bool localFallbackUsed = false)
    {
        return new RhodesCandidateApplyWorkflowResult(
            summary,
            apiError,
            localFallbackUsed,
            apiStatus,
            $"反映なし: 無視 {summary.IgnoredCount}件",
            string.IsNullOrWhiteSpace(apiError)
                ? $"状態へ反映できる候補はありませんでした。無視: {summary.IgnoredCount}件"
                : $"状態へ反映できる候補はありませんでした。API同期は失敗: {apiError}");
    }
}
