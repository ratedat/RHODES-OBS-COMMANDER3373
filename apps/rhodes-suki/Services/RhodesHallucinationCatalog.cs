using System.Text.Json;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesHallucinationCatalog
{
    public static SukiHallucinationCatalogSnapshot LoadDefault()
    {
        var path = Path.Combine(RhodesRunCatalog.ResolveDataRoot(), "hallucinations.json");
        if (!File.Exists(path))
            return new([], [], "", "", "");

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var source = root.TryGetProperty("source", out var sourceElement) ? sourceElement : default;
        var options = root.TryGetProperty("options", out var optionElements) && optionElements.ValueKind == JsonValueKind.Array
            ? optionElements.EnumerateArray()
                .OrderBy(item => JsonInt(item, "order"))
                .Select(item => new SukiHallucinationOption(
                    JsonString(item, "id"),
                    JsonString(item, "name"),
                    JsonString(item, "mapLabel"),
                    JsonString(item, "effect"),
                    JsonString(item, "flavorText"),
                    JsonString(item, "category"),
                    JsonStrings(item, "aliases")))
                .Where(item => !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
                .ToArray()
            : [];
        var fusions = root.TryGetProperty("fusions", out var fusionElements) && fusionElements.ValueKind == JsonValueKind.Array
            ? fusionElements.EnumerateArray()
                .Select(item => new SukiHallucinationFusion(
                    JsonString(item, "id"),
                    JsonStrings(item, "requiredIds"),
                    JsonString(item, "name"),
                    JsonString(item, "effect")))
                .Where(item => !string.IsNullOrWhiteSpace(item.Id) && item.RequiredIds.Count > 1)
                .ToArray()
            : [];

        return new(
            options,
            fusions,
            JsonString(source, "title"),
            JsonString(source, "url"),
            JsonString(source, "checkedAt"));
    }

    public static IReadOnlyList<string> NormalizeRecognizedNames(IEnumerable<string> values)
    {
        var catalog = LoadDefault();
        var result = new List<string>();
        foreach (var rawValue in values)
        {
            foreach (var value in SplitValues(rawValue))
            {
                var match = MatchOption(value, catalog.Options);
                if (match is not null && !result.Contains(match.Name, StringComparer.Ordinal))
                    result.Add(match.Name);
            }
        }
        return result;
    }

    public static IReadOnlyList<SukiHallucinationFusion> ResolveActiveFusions(IEnumerable<string> selectedNames)
    {
        var catalog = LoadDefault();
        var selectedIds = selectedNames
            .Select(name => MatchOption(name, catalog.Options)?.Id ?? "")
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);
        return catalog.Fusions
            .Where(fusion => fusion.RequiredIds.All(selectedIds.Contains))
            .ToArray();
    }

    private static SukiHallucinationOption? MatchOption(
        string value,
        IReadOnlyList<SukiHallucinationOption> options)
    {
        var normalized = NormalizeForMatch(value);
        if (normalized.Length < 2)
            return null;

        foreach (var option in options)
        {
            if (OptionTokens(option).Any(token => NormalizeForMatch(token).Equals(normalized, StringComparison.Ordinal)))
                return option;
        }

        var trimmed = TrimMapGrammar(normalized);
        foreach (var option in options)
        {
            if (OptionTokens(option).Any(token => TrimMapGrammar(NormalizeForMatch(token)).Equals(trimmed, StringComparison.Ordinal)))
                return option;
        }

        return options
            .Select(option => new
            {
                Option = option,
                Distance = OptionTokens(option)
                    .Select(token => EditDistance(trimmed, TrimMapGrammar(NormalizeForMatch(token))))
                    .DefaultIfEmpty(int.MaxValue)
                    .Min(),
            })
            .Where(candidate => candidate.Distance <= 1)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Option.Name, StringComparer.Ordinal)
            .Select(candidate => candidate.Option)
            .FirstOrDefault();
    }

    private static IEnumerable<string> OptionTokens(SukiHallucinationOption option)
    {
        yield return option.Name;
        if (!string.IsNullOrWhiteSpace(option.MapLabel))
            yield return option.MapLabel;
        foreach (var alias in option.Aliases)
            yield return alias;
    }

    private static IEnumerable<string> SplitValues(string value) =>
        (value ?? "").Split(
            ['/', '／', ',', '、', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string NormalizeForMatch(string value) =>
        new((value ?? "")
            .Normalize()
            .Where(char.IsLetterOrDigit)
            .ToArray());

    private static string TrimMapGrammar(string value)
    {
        foreach (var suffix in new[] { "的な", "した", "な", "の" })
        {
            if (value.EndsWith(suffix, StringComparison.Ordinal) && value.Length > suffix.Length)
                return value[..^suffix.Length];
        }
        return value;
    }

    private static int EditDistance(string left, string right)
    {
        if (left.Length == 0)
            return right.Length;
        if (right.Length == 0)
            return left.Length;

        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var substitution = previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1);
                current[j] = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), substitution);
            }
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private static string JsonString(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return "";
        return value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";
    }

    private static int JsonInt(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value))
            return 0;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    }

    private static IReadOnlyList<string> JsonStrings(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
            return [];
        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString() ?? "")
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }
}
