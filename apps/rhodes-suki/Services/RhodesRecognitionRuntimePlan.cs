using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRecognitionRuntimePlan
{
    private const string OperatorCardTemplateEntry = "RhodesTemplate_operatorsFull_operator_card_name";
    private const string RunStatusSquadIconEntryPrefix = "RhodesTemplate_runStatusFull_run_squad_icon_";
    private const string PhantomCampaignId = "is2_phantom";
    private const int RelicVisibleItemCapacity = 9;

    public static MaaResourceExecutionPlan PreparePreNavigation(MaaResourceExecutionPlan plan)
    {
        var selectedEntries = plan.ProfileId switch
        {
            "relicsFull" => plan.TaskEntries
                .Where(entry =>
                    entry.Equals(RhodesRelicFooterAvailabilityReader.MapFooterEntry, StringComparison.Ordinal)
                    || entry.Equals(RhodesRelicOwnedCountReader.Entry, StringComparison.Ordinal)
                    || entry.Equals(RhodesOperatorOwnedCountReader.Entry, StringComparison.Ordinal)
                    || entry.Equals(RhodesRelicFooterAvailabilityReader.CountMarkerEntry, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            "operatorsFull" => plan.TaskEntries
                .Where(entry => entry.Equals(RhodesOperatorOwnedCountReader.Entry, StringComparison.Ordinal))
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            _ => [],
        };
        var selected = plan.Tasks
            .Where(task => selectedEntries.Contains(task.Entry, StringComparer.Ordinal))
            .ToArray();
        return plan with { TaskEntries = selectedEntries, Tasks = selected };
    }

    public static MaaResourceExecutionPlan PrepareInitial(
        MaaResourceExecutionPlan plan,
        string? activeCampaignId = null)
    {
        var entries = plan.ProfileId switch
        {
            "runStatusFull" => FocusRunStatusEntries(plan.TaskEntries, activeCampaignId),
            "operatorsFull" => plan.TaskEntries.Where(entry =>
                entry.Equals(OperatorCardTemplateEntry, StringComparison.Ordinal)),
            "relicsFull" => plan.TaskEntries.Where(entry =>
                entry.Equals("RhodesScreen_relic_list", StringComparison.Ordinal)
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

    private static IEnumerable<string> FocusRunStatusEntries(
        IEnumerable<string> entries,
        string? activeCampaignId)
    {
        var campaignToken = activeCampaignId switch
        {
            "is2_phantom" => "_is2_phantom_",
            "is3_mizuki" => "_is3_mizuki_",
            // IS4 has no icon-template task and resolves the squad from common OCR.
            "is4_sami" => "__no_campaign_squad_icon__",
            "is5_sarkaz" => "_is5_sarkaz_",
            "is6_sui" => "_is6_sui_",
            _ => "",
        };
        if (string.IsNullOrWhiteSpace(campaignToken))
            return entries;

        return entries.Where(entry =>
            !entry.StartsWith(RunStatusSquadIconEntryPrefix, StringComparison.Ordinal)
            || entry.Contains(campaignToken, StringComparison.Ordinal));
    }

    public static bool IsScrollProfile(string profileId) =>
        profileId is "operatorsFull" or "relicsFull" or "is4RevelationFull" or "is4ParadigmLost" or "is5ThoughtFull" or "is6ActiveCoinsFull" or "is6CoinsFull";

    public static bool ShouldDeferInitialCoinStatusRecognition(string profileId) =>
        profileId.Equals("is6CoinsFull", StringComparison.Ordinal);

    public static bool ShouldCollectNormalizedEndpoint(
        string profileId,
        bool currentPassCollects,
        bool nextPassCollects) =>
        (profileId is "is6ActiveCoinsFull" or "is6CoinsFull")
        && !currentPassCollects
        && nextPassCollects;

    public static bool ShouldSkipScroll(
        string profileId,
        int initialCandidateCount,
        int? expectedCandidateCount = null,
        int? resolvedOperatorCardCount = null) =>
        HasReachedExpectedCandidateCount(
            profileId,
            initialCandidateCount,
            expectedCandidateCount,
            resolvedOperatorCardCount);

    public static bool IsKnownNonScrollableRelicList(
        string profileId,
        int? expectedCandidateCount,
        string campaignId) =>
        profileId == "relicsFull"
        && !campaignId.Equals(PhantomCampaignId, StringComparison.Ordinal)
        && expectedCandidateCount is >= 0 and <= RelicVisibleItemCapacity;

    public static bool ShouldRetryRelicFrameWithoutScroll(
        string profileId,
        int candidateCount,
        int? expectedCandidateCount,
        string campaignId) =>
        IsKnownNonScrollableRelicList(profileId, expectedCandidateCount, campaignId)
        && candidateCount < expectedCandidateCount;

    public static bool ShouldStopBeforeRelicScroll(
        string profileId,
        int candidateCount,
        int? expectedCandidateCount,
        string campaignId)
    {
        if (IsKnownNonScrollableRelicList(profileId, expectedCandidateCount, campaignId))
            return true;

        return profileId == "relicsFull"
            && !campaignId.Equals(PhantomCampaignId, StringComparison.Ordinal)
            && expectedCandidateCount is null
            && candidateCount is > 0 and < RelicVisibleItemCapacity;
    }

    public static bool ShouldEndRelicPassAfterImmobileProbe(
        string profileId,
        string campaignId,
        int executedScrolls,
        int fingerprintDistance) =>
        profileId == "relicsFull"
        && !campaignId.Equals(PhantomCampaignId, StringComparison.Ordinal)
        && executedScrolls == 1
        && fingerprintDistance <= 2;

    public static bool HasReachedExpectedCandidateCount(
        string profileId,
        int candidateCount,
        int? expectedCandidateCount,
        int? resolvedOperatorCardCount = null)
    {
        if (expectedCandidateCount is null or < 0
            || candidateCount != expectedCandidateCount.Value)
        {
            return false;
        }

        if (profileId == "relicsFull")
            return true;

        if (profileId == "is6ActiveCoinsFull")
            return true;

        return profileId == "operatorsFull"
            && resolvedOperatorCardCount >= expectedCandidateCount.Value;
    }

    public static int CountOperatorRosterCandidates(
        IEnumerable<MaaCandidatePreview> candidates)
    {
        return RhodesMaaCandidateMerger.Merge([], candidates)
            .Where(candidate => candidate.Kind.Equals("operator", StringComparison.OrdinalIgnoreCase))
            .Sum(candidate => Math.Max(1, candidate.Count));
    }

    public static bool IsTargetScreenConfirmed(
        string profileId,
        IEnumerable<MaaTaskRunResult> taskResults,
        string? activeCampaignId = null)
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
                && RhodesMaaLocalCandidateConverter.FromTaskResults(
                    profileId,
                    relicOcrResults,
                    activeCampaignId).Count > 0;
        }

        if (profileId == "is6CoinsFull")
        {
            var coinOcrResults = results.Where(result =>
                result.Succeeded
                && result.Hit
                && (result.Entry.Equals("RhodesOcrRegion_is6_coin_list_text", StringComparison.Ordinal)
                    || result.Entry.Contains("is6.coin_list_text", StringComparison.Ordinal))
                && result.Algorithm.Equals("OCR", StringComparison.OrdinalIgnoreCase));
            return RhodesMaaLocalCandidateConverter.FromTaskResults(
                    profileId,
                    coinOcrResults,
                    "is6_sui")
                .Any(candidate => candidate.Kind.Equals("coin", StringComparison.Ordinal));
        }

        if (profileId is "is4RevelationFull" or "is4ParadigmLost")
        {
            var fieldId = profileId == "is4RevelationFull" ? "revelation" : "paradigmLost";
            return RhodesMaaLocalCandidateConverter.FromTaskResults(
                    profileId,
                    results,
                    "is4_sami")
                .Any(candidate => candidate.FieldId.Equals(fieldId, StringComparison.Ordinal));
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

    public static bool CanContinueAfterUnconfirmedTarget(string profileId) =>
        profileId.Equals("is5AgeFull", StringComparison.Ordinal);

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

    public static bool CanStopResolvedOperatorViewport(
        string profileId,
        int executedScrolls,
        int minScrolls,
        int stableFrameCount,
        int fingerprintStableCount,
        bool trackerCanStop)
    {
        return profileId.Equals("operatorsFull", StringComparison.Ordinal)
            && trackerCanStop
            && executedScrolls >= minScrolls;
    }

    public static bool CanStopResolvedOperatorScan(
        string profileId,
        int completedPassCount,
        int totalPassCount,
        bool trackerCanStop)
    {
        return profileId.Equals("operatorsFull", StringComparison.Ordinal)
            && trackerCanStop
            && totalPassCount > 0
            && completedPassCount >= totalPassCount;
    }
}
