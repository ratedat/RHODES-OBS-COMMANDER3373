using System.Globalization;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesOperatorOwnedCountEvidence(int Count, string RawText, double Confidence);

public static class RhodesOperatorOwnedCountReader
{
    public const string Entry = "RhodesOcrRegion_run_operator_count";
    private const double MinimumConfidence = 0.98;

    public static RhodesOperatorOwnedCountEvidence? FromTaskResults(
        IEnumerable<MaaTaskRunResult> taskResults,
        int? previousCount = null)
    {
        var results = taskResults as MaaTaskRunResult[] ?? taskResults.ToArray();
        var matching = results.Where(result =>
            result.Entry.Equals(Entry, StringComparison.Ordinal)
            && result.Succeeded
            && result.Hit);
        return RhodesMaaOcrDetailRows.FromTaskResults(matching)
            .Where(row => (row.Score ?? 0) >= MinimumConfidence)
            .Select(row => TryParse(row.Text, out var count)
                && IsPlausibleTransition(previousCount, count)
                ? new RhodesOperatorOwnedCountEvidence(count, row.Text.Trim(), row.Score ?? 0)
                : null)
            .Where(evidence => evidence is not null)
            .OrderByDescending(evidence => evidence!.Confidence)
            .FirstOrDefault();
    }

    private static bool IsPlausibleTransition(int? previousCount, int count) =>
        previousCount is null
        || previousCount < 0
        || count == previousCount
        || count == previousCount + 1;

    private static bool TryParse(string text, out int count)
    {
        count = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var normalized = new string(text
            .Where(character => !char.IsWhiteSpace(character))
            .Select(NormalizeDigit)
            .ToArray());
        return normalized.Length is 1 or 2
            && normalized.All(char.IsDigit)
            && int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out count)
            && count is >= 1 and <= 99;
    }

    private static char NormalizeDigit(char value) =>
        value is >= '０' and <= '９'
            ? (char)('0' + value - '０')
            : value;
}
