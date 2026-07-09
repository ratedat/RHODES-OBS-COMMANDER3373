using System.Text.Json;

namespace RhodesSuki.Services;

/// <summary>
/// 等級(run.difficulty)から多元化珍品の効果バリアントtier(run.difficultyTierId)を導出する。
/// 規則は data/difficulty-tiers.json と Web側 app/domain/difficulty.js の applyDifficultyTier に一致させる:
/// minDifficulty &lt;= difficulty かつ (maxDifficulty が null または difficulty &lt;= maxDifficulty) の最初のtier。
/// </summary>
public static class RhodesDifficultyTierCatalog
{
    private static readonly Lazy<IReadOnlyDictionary<string, CampaignTiers>> Catalog = new(Load);

    /// <summary>tier定義のないキャンペーンでは空文字を返す (stateへ書かない)。</summary>
    public static string ResolveTierId(string? campaignId, int difficulty)
    {
        if (string.IsNullOrWhiteSpace(campaignId) || !Catalog.Value.TryGetValue(campaignId, out var config))
            return "";

        var tier = config.Tiers.FirstOrDefault(item =>
            difficulty >= item.MinDifficulty
            && (item.MaxDifficulty is null || difficulty <= item.MaxDifficulty));
        return tier?.Id ?? config.DefaultTierId;
    }

    public static string TierLabel(string? campaignId, string? tierId)
    {
        if (string.IsNullOrWhiteSpace(campaignId)
            || string.IsNullOrWhiteSpace(tierId)
            || !Catalog.Value.TryGetValue(campaignId, out var config))
        {
            return "";
        }

        return config.Tiers.FirstOrDefault(item => item.Id.Equals(tierId, StringComparison.Ordinal))?.Label ?? tierId;
    }

    /// <summary>手動入力UI向け: 「幻想的 (多元化珍品)」のような説明ラベル。定義が無ければ空。</summary>
    public static string DescribeTier(string? campaignId, int difficulty)
    {
        var tierId = ResolveTierId(campaignId, difficulty);
        if (string.IsNullOrWhiteSpace(tierId))
            return "";

        var label = TierLabel(campaignId, tierId);
        return string.IsNullOrWhiteSpace(label) ? "" : $"多元化珍品: {label}";
    }

    public static bool HasTiers(string? campaignId)
    {
        return !string.IsNullOrWhiteSpace(campaignId) && Catalog.Value.ContainsKey(campaignId);
    }

    private static IReadOnlyDictionary<string, CampaignTiers> Load()
    {
        try
        {
            var path = Path.Combine(RhodesRunCatalog.ResolveDataRoot(), "difficulty-tiers.json");
            if (!File.Exists(path))
                return new Dictionary<string, CampaignTiers>(StringComparer.Ordinal);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("campaignDifficultyTiers", out var campaigns)
                || campaigns.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, CampaignTiers>(StringComparer.Ordinal);
            }

            var result = new Dictionary<string, CampaignTiers>(StringComparer.Ordinal);
            foreach (var campaign in campaigns.EnumerateObject())
            {
                var config = campaign.Value;
                if (!config.TryGetProperty("tiers", out var tiers) || tiers.ValueKind != JsonValueKind.Array)
                    continue;

                var parsed = tiers.EnumerateArray()
                    .Select(tier => new TierEntry(
                        JsonString(tier, "id"),
                        JsonString(tier, "label"),
                        JsonInt(tier, "minDifficulty") ?? 0,
                        JsonInt(tier, "maxDifficulty")))
                    .Where(tier => !string.IsNullOrWhiteSpace(tier.Id))
                    .ToArray();
                if (parsed.Length == 0)
                    continue;

                result[campaign.Name] = new CampaignTiers(
                    JsonString(config, "defaultTierId"),
                    parsed);
            }

            return result;
        }
        catch
        {
            // tierデータが読めなくても他機能を止めない。導出はスキップされる。
            return new Dictionary<string, CampaignTiers>(StringComparer.Ordinal);
        }
    }

    private static string JsonString(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";
    }

    private static int? JsonInt(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private sealed record CampaignTiers(string DefaultTierId, IReadOnlyList<TierEntry> Tiers);

    private sealed record TierEntry(string Id, string Label, int MinDifficulty, int? MaxDifficulty);
}
