using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RhodesSuki.Services;

public static class RhodesOperatorOcrNormalizer
{
    private static readonly Lazy<IReadOnlyDictionary<char, char>> EquivalenceMap = new(LoadEquivalenceMap);
    private static readonly Lazy<IReadOnlyList<OfficialOperatorRule>> OfficialRules = new(LoadOfficialRules);
    private static readonly IReadOnlyDictionary<string, string> MeasuredAliases =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["フメイ"] = "メイ",
            ["プメイ"] = "メイ",
            ["ユリチニル"] = "ムリナール",
            ["アラコーデイア"] = "トラゴーデイア",
            ["アラコデイア"] = "トラゴーデイア",
            ["下ラコーデイア"] = "トラゴーデイア",
            ["下ラコデイア"] = "トラゴーデイア",
        };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        var normalized = value.Trim().Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        foreach (var original in normalized)
        {
            if (char.IsWhiteSpace(original))
                continue;
            var ch = HiraganaToKatakana(original);
            if (EquivalenceMap.Value.TryGetValue(ch, out var replacement))
                ch = replacement;
            if (char.IsLetterOrDigit(ch) || ch is 'ー')
                builder.Append(char.ToLowerInvariant(ch));
        }

        var result = builder.ToString().TrimStart('ー');
        return MeasuredAliases.TryGetValue(result.TrimEnd('ー'), out var alias)
            ? alias
            : result;
    }

    public static string? ResolveOfficialOperatorId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var raw = value.Trim().Normalize(NormalizationForm.FormKC);
        var compact = string.Concat(raw.Where(ch => !char.IsWhiteSpace(ch)));
        foreach (var rule in OfficialRules.Value)
        {
            try
            {
                if (rule.Pattern.IsMatch(raw)
                    || !string.Equals(raw, compact, StringComparison.Ordinal) && rule.Pattern.IsMatch(compact))
                {
                    return rule.OperatorId;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // A pathological upstream rule must not block recognition of the remaining names.
            }
        }
        return null;
    }

    private static IReadOnlyDictionary<char, char> LoadEquivalenceMap()
    {
        var result = new Dictionary<char, char>();
        var path = Path.Combine(AppContext.BaseDirectory, "data", "recognition", "maa-operator-name-ocr.json");
        if (!File.Exists(path))
            return result;

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("equivalenceClasses", out var classes)
                || classes.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var group in classes.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Array))
            {
                var variants = group.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetString() ?? "")
                    .Where(item => item.Length == 1)
                    .Select(item => HiraganaToKatakana(item[0]))
                    .Distinct()
                    .ToArray();
                if (variants.Length < 2)
                    continue;
                var replacement = variants.Contains('ー') ? 'ー' : variants[0];
                foreach (var variant in variants)
                    result[variant] = replacement;
            }
        }
        catch
        {
            return new Dictionary<char, char>();
        }
        return result;
    }

    private static IReadOnlyList<OfficialOperatorRule> LoadOfficialRules()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "data", "recognition", "maa-operator-name-ocr.json");
        if (!File.Exists(path))
            return [];

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("rules", out var rules)
                || rules.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var result = new List<OfficialOperatorRule>();
            foreach (var rule in rules.EnumerateArray())
            {
                if (rule.ValueKind != JsonValueKind.Object
                    || rule.TryGetProperty("validRegex", out var validRegex) && validRegex.ValueKind == JsonValueKind.False
                    || !rule.TryGetProperty("pattern", out var patternNode)
                    || patternNode.ValueKind != JsonValueKind.String
                    || !rule.TryGetProperty("localMatches", out var localMatches)
                    || localMatches.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                var operatorIds = localMatches.EnumerateArray()
                    .Where(item => item.ValueKind == JsonValueKind.Object
                                   && item.TryGetProperty("id", out var id)
                                   && id.ValueKind == JsonValueKind.String)
                    .Select(item => item.GetProperty("id").GetString() ?? "")
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                if (operatorIds.Length != 1)
                    continue;

                var pattern = patternNode.GetString();
                if (string.IsNullOrWhiteSpace(pattern))
                    continue;
                try
                {
                    result.Add(new OfficialOperatorRule(
                        new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(25)),
                        operatorIds[0]));
                }
                catch (ArgumentException)
                {
                    // The source file also records regex validity, but keep loading resilient to engine differences.
                }
            }
            return result;
        }
        catch
        {
            return [];
        }
    }

    private static char HiraganaToKatakana(char ch)
    {
        return ch is >= '\u3041' and <= '\u3096'
            ? (char)(ch + 0x60)
            : ch;
    }

    private sealed record OfficialOperatorRule(Regex Pattern, string OperatorId);
}
