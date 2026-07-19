using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionRuntimePlan
{
    private const string OperatorCardTemplateEntry = "RhodesTemplate_operatorsFull_operator_card_name";

    public static MaaResourceExecutionPlan PrepareInitial(MaaResourceExecutionPlan plan)
    {
        var entries = plan.ProfileId switch
        {
            "operatorsFull" => plan.TaskEntries.Where(entry =>
                entry.Equals(OperatorCardTemplateEntry, StringComparison.Ordinal)),
            "relicsFull" => plan.TaskEntries.Where(entry =>
                entry.Equals(RhodesRelicOwnedCountReader.Entry, StringComparison.Ordinal)
                || entry.Equals("RhodesScreen_relic_list", StringComparison.Ordinal)
                || entry.Equals("RhodesOcrRegion_relic_list_text", StringComparison.Ordinal)
                || entry.Equals("RhodesOcrRegion_relic_detail_name", StringComparison.Ordinal)),
            _ => plan.TaskEntries,
        };
        var selectedEntries = entries.Distinct(StringComparer.Ordinal).ToArray();
        if (selectedEntries.Length == 0 || selectedEntries.Length == plan.TaskEntries.Count)
            return plan;

        var selected = plan.Tasks
            .Where(task => selectedEntries.Contains(task.Entry, StringComparer.Ordinal))
            .ToArray();
        return plan with { TaskEntries = selectedEntries, Tasks = selected };
    }

    public static bool IsScrollProfile(string profileId) =>
        profileId is "operatorsFull" or "relicsFull" or "is5ThoughtFull";

    public static bool ShouldSkipScroll(
        string profileId,
        int initialCandidateCount,
        int? expectedCandidateCount = null) =>
        HasReachedExpectedCandidateCount(profileId, initialCandidateCount, expectedCandidateCount);

    public static bool HasReachedExpectedCandidateCount(
        string profileId,
        int candidateCount,
        int? expectedCandidateCount) =>
        profileId == "relicsFull"
        && expectedCandidateCount is >= 0
        && candidateCount == expectedCandidateCount.Value;

    public static bool IsTargetScreenConfirmed(
        string profileId,
        IEnumerable<MaaTaskRunResult> taskResults)
    {
        var results = taskResults as MaaTaskRunResult[] ?? taskResults.ToArray();
        if (profileId == "relicsFull")
        {
            if (results.Any(result =>
                result.Succeeded
                && result.Hit
                && result.Entry.Equals("RhodesScreen_relic_list", StringComparison.Ordinal)))
            {
                return true;
            }

            var relicOcrResults = results
                .Where(result =>
                    result.Succeeded
                    && result.Hit
                    && (result.Entry.Equals("RhodesOcrRegion_relic_list_text", StringComparison.Ordinal)
                        || result.Entry.Equals("RhodesOcrRegion_relic_detail_name", StringComparison.Ordinal))
                    && result.Algorithm.Equals("OCR", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            return relicOcrResults.Length > 0
                && RhodesMaaLocalCandidateConverter.FromTaskResults(profileId, relicOcrResults).Count > 0;
        }

        var requiredEntry = profileId switch
        {
            "operatorsFull" => OperatorCardTemplateEntry,
            "is5AgeFull" => "RhodesScreen_run_sarkaz_age_detail",
            _ => "",
        };
        if (string.IsNullOrWhiteSpace(requiredEntry))
            return true;

        return results.Any(result =>
            result.Entry.Equals(requiredEntry, StringComparison.Ordinal)
            && result.Succeeded
            && result.Hit
            && (profileId != "operatorsFull"
                || result.Algorithm.Equals("TemplateMatch", StringComparison.OrdinalIgnoreCase)));
    }

    public static bool HasReachedScrollEnd(
        int executedScrolls,
        int minScrolls,
        int stableFrameCount,
        int fingerprintStableCount,
        int stableCandidateCount,
        int candidateStableCount)
    {
        if (executedScrolls < minScrolls)
            return false;
        if (stableFrameCount >= fingerprintStableCount)
            return true;

        return candidateStableCount > 0
            && stableCandidateCount >= candidateStableCount
            && stableFrameCount > 0;
    }
}
