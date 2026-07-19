using System.Globalization;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesRelicOwnedCountEvidence(int Count, string RawText, double Confidence);

public static class RhodesRelicOwnedCountReader
{
    public const string Entry = "RhodesScreen_run_map_footer_relic";

    public static RhodesRelicOwnedCountEvidence? FromTaskResults(IEnumerable<MaaTaskRunResult> taskResults)
    {
        var matching = taskResults
            .Where(result => result.Entry.Equals(Entry, StringComparison.Ordinal)
                && result.Succeeded
                && result.Hit)
            .ToArray();
        return RhodesMaaOcrDetailRows.FromTaskResults(matching)
            .Select(row => TryParse(row.Text, out var count)
                ? new RhodesRelicOwnedCountEvidence(count, row.Text.Trim(), row.Score ?? 0)
                : null)
            .Where(evidence => evidence is not null)
            .OrderByDescending(evidence => evidence!.Confidence)
            .FirstOrDefault();
    }

    private static bool TryParse(string text, out int count)
    {
        count = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var digits = new string(text.Select(NormalizeDigit).Where(char.IsDigit).ToArray());
        return digits.Length is > 0 and <= 3
            && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out count)
            && count is >= 0 and <= 999;
    }

    private static char NormalizeDigit(char value)
    {
        if (value is >= '０' and <= '９')
            return (char)('0' + value - '０');
        return value switch
        {
            'O' or 'o' => '0',
            'I' or 'l' or '|' => '1',
            _ => value,
        };
    }
}
