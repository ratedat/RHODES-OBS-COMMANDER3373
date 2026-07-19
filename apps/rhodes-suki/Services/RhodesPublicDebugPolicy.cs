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
        return !string.IsNullOrWhiteSpace(campaignId);
    }

    public static bool IsCampaignAllowed(string? campaignId, RhodesDistributionProfile distributionProfile)
    {
        return !distributionProfile.IsPublicDebug || IsCampaignAllowed(campaignId);
    }

    public static bool IsProfileAllowed(string? profileId)
    {
        return !string.IsNullOrWhiteSpace(profileId) && AllowedProfileIds.Contains(profileId);
    }

    public static bool IsProfileAllowed(string? profileId, RhodesDistributionProfile distributionProfile)
    {
        return !distributionProfile.IsPublicDebug || IsProfileAllowed(profileId);
    }

    public static SukiRunStateSnapshot ApplyCampaign(SukiRunStateSnapshot state)
    {
        return state;
    }

    public static SukiRunStateSnapshot ApplyCampaign(
        SukiRunStateSnapshot state,
        RhodesDistributionProfile distributionProfile)
    {
        return distributionProfile.IsPublicDebug ? ApplyCampaign(state) : state;
    }

    public static IReadOnlyList<SukiCampaignPreview> FilterCampaigns(IEnumerable<SukiCampaignPreview> campaigns)
    {
        return campaigns.ToArray();
    }

    public static IReadOnlyList<SukiCampaignPreview> FilterCampaigns(
        IEnumerable<SukiCampaignPreview> campaigns,
        RhodesDistributionProfile distributionProfile)
    {
        var available = campaigns.ToArray();
        return distributionProfile.IsPublicDebug ? FilterCampaigns(available) : available;
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

    public static IReadOnlyList<MaaResourceProfilePreview> FilterProfiles(
        IEnumerable<MaaResourceProfilePreview> profiles,
        RhodesDistributionProfile distributionProfile)
    {
        var available = profiles.ToArray();
        return distributionProfile.IsPublicDebug ? FilterProfiles(available) : available;
    }
}
