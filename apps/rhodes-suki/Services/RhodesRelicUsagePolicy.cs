using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRelicUsagePolicy
{
    private static readonly HashSet<string> TrackableNames = new(StringComparer.Ordinal)
    {
        "「時の果て」",
        "「門」と「救難」",
    };

    public static bool SupportsUsedFlag(string? name)
    {
        return !string.IsNullOrWhiteSpace(name) && TrackableNames.Contains(name.Trim());
    }

    public static int OwnedDisplayPriority(SukiChoiceItem item)
    {
        return item.IsSelected && item.SupportsUsedFlag ? 0 : 1;
    }
}
