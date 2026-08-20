using System.Text.Json;

namespace RhodesSuki.Services;

public sealed record RhodesRelicStackRule(
    string RelicId,
    string CampaignId,
    string Name,
    int? Maximum);

public sealed record RhodesRelicNonStackRule(
    string RelicId,
    string CampaignId,
    string Name,
    string Reason);

public static class RhodesRelicStackRuleCatalog
{
    private static readonly Lazy<IReadOnlyList<RhodesRelicStackRule>> DefaultRules =
        new(() => LoadFromPath(Path.Combine(RhodesRunCatalog.ResolveDataRoot(), "relic-stack-rules.json")));
    private static readonly Lazy<IReadOnlyDictionary<string, RhodesRelicStackRule>> DefaultById =
        new(() => DefaultRules.Value.ToDictionary(rule => rule.RelicId, StringComparer.Ordinal));
    private static readonly Lazy<IReadOnlyList<RhodesRelicNonStackRule>> DefaultNonStackRelics =
        new(() => LoadNonStackRelicsFromPath(Path.Combine(RhodesRunCatalog.ResolveDataRoot(), "relic-stack-rules.json")));
    private static readonly Lazy<IReadOnlySet<string>> DefaultNonStackIds =
        new(() => DefaultNonStackRelics.Value.Select(rule => rule.RelicId).ToHashSet(StringComparer.Ordinal));

    public static IReadOnlyList<RhodesRelicStackRule> LoadDefault(string dataRootOverride = "")
    {
        if (string.IsNullOrWhiteSpace(dataRootOverride))
            return DefaultRules.Value;

        return LoadFromPath(Path.Combine(dataRootOverride, "relic-stack-rules.json"));
    }

    public static RhodesRelicStackRule? Find(string relicId)
    {
        return !string.IsNullOrWhiteSpace(relicId)
            && DefaultById.Value.TryGetValue(relicId, out var rule)
                ? rule
                : null;
    }

    public static IReadOnlyList<RhodesRelicNonStackRule> LoadExplicitNonStackRelics(string dataRootOverride = "")
    {
        if (string.IsNullOrWhiteSpace(dataRootOverride))
            return DefaultNonStackRelics.Value;

        return LoadNonStackRelicsFromPath(Path.Combine(dataRootOverride, "relic-stack-rules.json"));
    }

    public static bool IsExplicitlyNonStack(string relicId, string dataRootOverride = "")
    {
        if (string.IsNullOrWhiteSpace(relicId))
            return false;

        if (string.IsNullOrWhiteSpace(dataRootOverride))
            return DefaultNonStackIds.Value.Contains(relicId);

        return LoadExplicitNonStackRelics(dataRootOverride)
            .Any(rule => rule.RelicId.Equals(relicId, StringComparison.Ordinal));
    }

    public static bool IsWithinKnownLimit(string relicId, int count)
    {
        if (count <= 0)
            return false;
        if (IsExplicitlyNonStack(relicId))
            return false;

        var rule = Find(relicId);
        return rule?.Maximum is not int maximum || count <= maximum;
    }

    public static int ClampManualCount(string relicId, int count)
    {
        if (count <= 0)
            return 0;
        if (IsExplicitlyNonStack(relicId))
            return 0;

        var maximum = Find(relicId)?.Maximum;
        return maximum is int value ? Math.Min(count, value) : count;
    }

    private static IReadOnlyList<RhodesRelicStackRule> LoadFromPath(string path)
    {
        if (!File.Exists(path))
            return [];

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("rules", out var rules)
            || rules.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return rules.EnumerateArray()
            .Select(item => new RhodesRelicStackRule(
                JsonString(item, "relicId"),
                JsonString(item, "campaignId"),
                JsonString(item, "name"),
                JsonNullableInt(item, "maximum")))
            .Where(rule => !string.IsNullOrWhiteSpace(rule.RelicId)
                && !string.IsNullOrWhiteSpace(rule.CampaignId)
                && !string.IsNullOrWhiteSpace(rule.Name)
                && (rule.Maximum is null || rule.Maximum > 0))
            .GroupBy(rule => rule.RelicId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static IReadOnlyList<RhodesRelicNonStackRule> LoadNonStackRelicsFromPath(string path)
    {
        if (!File.Exists(path))
            return [];

        using var document = JsonDocument.Parse(File.ReadAllText(path));
        if (!document.RootElement.TryGetProperty("nonStackRelics", out var relics)
            || relics.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return relics.EnumerateArray()
            .Select(item => new RhodesRelicNonStackRule(
                JsonString(item, "relicId"),
                JsonString(item, "campaignId"),
                JsonString(item, "name"),
                JsonString(item, "reason")))
            .Where(rule => !string.IsNullOrWhiteSpace(rule.RelicId)
                && !string.IsNullOrWhiteSpace(rule.CampaignId)
                && !string.IsNullOrWhiteSpace(rule.Name))
            .GroupBy(rule => rule.RelicId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
    }

    private static string JsonString(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
    }

    private static int? JsonNullableInt(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
                ? number
                : null;
    }
}
