using System.Globalization;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public sealed record RhodesSuiActiveCoinCountEvidence(int Count, string RawText, double Confidence);

public static class RhodesSuiActiveCoinCountReader
{
    public const string Entry = "RhodesOcrRegion_is6_active_coin_count";

    public static RhodesSuiActiveCoinCountEvidence? FromTaskResults(
        IEnumerable<MaaTaskRunResult> taskResults)
    {
        var matching = taskResults.Where(result =>
            result.Entry.Equals(Entry, StringComparison.Ordinal)
            && result.Succeeded
            && result.Hit);
        return RhodesMaaOcrDetailRows.FromTaskResults(matching)
            .Select(row => TryParse(row.Text, out var count)
                ? new RhodesSuiActiveCoinCountEvidence(count, row.Text.Trim(), row.Score ?? 0)
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

        var normalized = new string(text
            .Select(NormalizeDigit)
            .ToArray())
            .Trim();
        if (normalized.Any(value => !char.IsDigit(value) && !IsIgnorableOcrNoise(value)))
            return false;

        var digits = new string(normalized
            .Where(char.IsDigit)
            .ToArray());
        return digits.Length is 1 or 2
            && int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out count)
            && count is >= 1 and <= 99;
    }

    private static bool IsIgnorableOcrNoise(char value) =>
        char.IsWhiteSpace(value)
        || value is '.' or ',' or ':' or ';' or '-' or '_' or '\'' or '"' or '`' or '·' or '・';

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
