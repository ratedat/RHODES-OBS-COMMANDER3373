using RhodesSuki.Models;

namespace RhodesSuki.Services;

public enum RhodesRelicFooterAvailability
{
    Unknown,
    Empty,
    HasOwnedRelics,
}

public static class RhodesRelicFooterAvailabilityReader
{
    public const string MapFooterEntry = "RhodesScreen_run_map_footer";
    public const string CountMarkerEntry = "RhodesOcrRegion_run_relic_count_marker";
    private const double MinimumMarkerConfidence = 0.55;

    public static RhodesRelicFooterAvailability Evaluate(
        IEnumerable<MaaTaskRunResult> taskResults,
        byte[]? encodedImage = null)
    {
        var results = taskResults as MaaTaskRunResult[] ?? taskResults.ToArray();
        var mapConfirmed = results.Any(result =>
            result.Entry.Equals(MapFooterEntry, StringComparison.Ordinal)
            && result.Succeeded
            && result.Hit)
            || RhodesOperatorOwnedCountReader.FromTaskResults(results) is not null;
        if (!mapConfirmed)
            return RhodesRelicFooterAvailability.Unknown;

        var imageInspection = RhodesRelicFooterImageDetector.Inspect(encodedImage ?? []);
        if (imageInspection.Availability != RhodesRelicFooterAvailability.Unknown)
            return imageInspection.Availability;

        var markerResults = results
            .Where(result =>
                result.Entry.Equals(CountMarkerEntry, StringComparison.Ordinal)
                && result.Succeeded)
            .ToArray();
        if (markerResults.Length == 0)
            return RhodesRelicFooterAvailability.Unknown;

        var markerRows = RhodesMaaOcrDetailRows.FromTaskResults(markerResults)
            .Where(row => (row.Score ?? 0) >= MinimumMarkerConfidence)
            .ToArray();
        if (markerRows.Length == 0)
            return RhodesRelicFooterAvailability.Unknown;

        return markerRows.Any(row => row.Text.Any(IsDigit))
            ? RhodesRelicFooterAvailability.HasOwnedRelics
            : RhodesRelicFooterAvailability.Unknown;
    }

    private static bool IsDigit(char value) =>
        char.IsDigit(value) || value is >= '０' and <= '９';
}
