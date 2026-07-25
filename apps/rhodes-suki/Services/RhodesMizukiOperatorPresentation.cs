using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMizukiOperatorPresentation
{
    private const string MizukiCampaignId = "is3_mizuki";
    private const string SuiCampaignId = "is6_sui";
    private const string RejectionReactionFieldId = "rejectionReaction";
    private const string EvolutionFieldId = "operatorEvolution";
    private const string CandleBearerFieldId = "candleBearer";

    public static void Apply(
        string campaignId,
        IReadOnlyList<SukiSpecialFieldState>? specialFields,
        IEnumerable<SukiChoiceItem> operators)
    {
        var rejectionTargetIds = TargetIds(
            campaignId,
            MizukiCampaignId,
            specialFields,
            RejectionReactionFieldId);
        var evolutionTargetIds = TargetIds(
            campaignId,
            MizukiCampaignId,
            specialFields,
            EvolutionFieldId);
        var candleBearerTargetIds = TargetIds(
            campaignId,
            SuiCampaignId,
            specialFields,
            CandleBearerFieldId);

        foreach (var item in operators)
        {
            item.IsRejectionReactionTarget = rejectionTargetIds.Contains(item.Id);
            item.IsEvolutionTarget = evolutionTargetIds.Contains(item.Id);
            item.IsCandleBearerTarget = candleBearerTargetIds.Contains(item.Id);
        }
    }

    private static HashSet<string> TargetIds(
        string campaignId,
        string expectedCampaignId,
        IReadOnlyList<SukiSpecialFieldState>? specialFields,
        string fieldId)
    {
        if (!string.Equals(campaignId, expectedCampaignId, StringComparison.Ordinal))
            return [];

        var field = (specialFields ?? []).FirstOrDefault(candidate =>
            string.Equals(candidate.CampaignId, expectedCampaignId, StringComparison.Ordinal)
            && string.Equals(candidate.FieldId, fieldId, StringComparison.Ordinal));
        return field?.OperatorTargets?
            .Select(target => target.OperatorId)
            .Where(operatorId => !string.IsNullOrWhiteSpace(operatorId))
            .ToHashSet(StringComparer.Ordinal)
            ?? field?.OperatorIds?.ToHashSet(StringComparer.Ordinal)
            ?? [];
    }
}
