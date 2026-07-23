using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMizukiOperatorPresentation
{
    private const string CampaignId = "is3_mizuki";
    private const string RejectionReactionFieldId = "rejectionReaction";
    private const string EvolutionFieldId = "operatorEvolution";

    public static void Apply(
        string campaignId,
        IReadOnlyList<SukiSpecialFieldState>? specialFields,
        IEnumerable<SukiChoiceItem> operators)
    {
        var rejectionTargetIds = TargetIds(campaignId, specialFields, RejectionReactionFieldId);
        var evolutionTargetIds = TargetIds(campaignId, specialFields, EvolutionFieldId);

        foreach (var item in operators)
        {
            item.IsRejectionReactionTarget = rejectionTargetIds.Contains(item.Id);
            item.IsEvolutionTarget = evolutionTargetIds.Contains(item.Id);
        }
    }

    private static HashSet<string> TargetIds(
        string campaignId,
        IReadOnlyList<SukiSpecialFieldState>? specialFields,
        string fieldId)
    {
        if (!string.Equals(campaignId, CampaignId, StringComparison.Ordinal))
            return [];

        var field = (specialFields ?? []).FirstOrDefault(candidate =>
            string.Equals(candidate.CampaignId, CampaignId, StringComparison.Ordinal)
            && string.Equals(candidate.FieldId, fieldId, StringComparison.Ordinal));
        return field?.OperatorTargets?
            .Select(target => target.OperatorId)
            .Where(operatorId => !string.IsNullOrWhiteSpace(operatorId))
            .ToHashSet(StringComparer.Ordinal)
            ?? field?.OperatorIds?.ToHashSet(StringComparer.Ordinal)
            ?? [];
    }
}
