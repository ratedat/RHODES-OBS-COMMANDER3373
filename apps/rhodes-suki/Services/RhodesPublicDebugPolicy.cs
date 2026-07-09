using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesPublicDebugPolicy
{
    public const string SarkazCampaignId = "is5_sarkaz";

    private static readonly string[] ProfileOrder =
    [
        "runStatusFull",
        "operatorsFull",
        "relicsFull",
        "is5ThoughtFull",
        "is5AgeFull",
    ];

    private static readonly HashSet<string> AllowedProfileIds = new(ProfileOrder, StringComparer.Ordinal);

    public static IReadOnlyList<string> ProfileIds => ProfileOrder;

    public static bool IsCampaignAllowed(string? campaignId)
    {
        return string.Equals(campaignId, SarkazCampaignId, StringComparison.Ordinal);
    }

    public static bool IsProfileAllowed(string? profileId)
    {
        return !string.IsNullOrWhiteSpace(profileId) && AllowedProfileIds.Contains(profileId);
    }

    public static SukiRunStateSnapshot ApplyCampaign(SukiRunStateSnapshot state)
    {
        return IsCampaignAllowed(state.CampaignId)
            ? state
            : state with { CampaignId = SarkazCampaignId };
    }

    public static IReadOnlyList<SukiCampaignPreview> FilterCampaigns(IEnumerable<SukiCampaignPreview> campaigns)
    {
        var filtered = campaigns
            .Where(campaign => IsCampaignAllowed(campaign.Id))
            .ToArray();
        return filtered.Length > 0 ? filtered : campaigns.ToArray();
    }

    public static IReadOnlyList<MaaResourceProfilePreview> FilterProfiles(IEnumerable<MaaResourceProfilePreview> profiles)
    {
        var byId = profiles
            .Where(profile => IsProfileAllowed(profile.Id))
            .GroupBy(profile => profile.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        return ProfileOrder
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToArray();
    }
}
