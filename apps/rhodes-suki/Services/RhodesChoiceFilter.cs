using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesChoiceFilter
{
    public static IReadOnlyList<SukiChoiceItem> Apply(
        IEnumerable<SukiChoiceItem> items,
        SukiChoiceFilterOptions options)
    {
        var query = Normalize(options.SearchText);
        var filtered = items.Where(item =>
        {
            if (!options.IncludeHidden && item.HiddenByDefault)
                return false;
            if (options.HideExcluded && item.IsExcluded)
                return false;
            if (options.SelectedOnly && !item.IsSelected)
                return false;
            if (!string.IsNullOrWhiteSpace(options.CampaignId) && item.CampaignId != options.CampaignId)
                return false;
            if (!IsAll(options.Category) && item.Category != options.Category)
                return false;
            if (!IsAll(options.OperatorClass) && item.OperatorClass != options.OperatorClass)
                return false;
            if (!IsAll(options.OperatorBranch) && item.OperatorBranch != options.OperatorBranch)
                return false;
            if (!IsAll(options.Rarity) && item.Rarity.ToString() != options.Rarity.TrimStart('★'))
                return false;
            return string.IsNullOrWhiteSpace(query) || Normalize(item.SearchText).Contains(query, StringComparison.Ordinal);
        });

        var ordered = filtered.OrderBy(RhodesRelicUsagePolicy.OwnedDisplayPriority);
        return (options.ShowSelectedFirst
                ? ordered.ThenByDescending(item => item.IsSelected).ThenBy(item => item.SortOrder).ThenBy(item => item.Name, StringComparer.Ordinal)
                : ordered.ThenBy(item => item.SortOrder).ThenBy(item => item.Name, StringComparer.Ordinal))
            .ToArray();
    }

    // クリック直後の全再構築は「押した項目が並び替えで移動し、スクロール位置も失われる」ため、
    // 表示メンバーが実際に増減するフィルター(選択のみ/除外を隠す)に限定する。
    // 優先表示(選択を先頭へ)の並び替えは、次にリストが再構築されるタイミングで反映される。
    public static bool RequiresFullRefreshAfterSelectionMutation(SukiChoiceFilterOptions options)
    {
        return options.SelectedOnly;
    }

    public static bool RequiresFullRefreshAfterExclusionMutation(SukiChoiceFilterOptions options)
    {
        return options.HideExcluded || options.SelectedOnly;
    }

    private static bool IsAll(string value)
    {
        return string.IsNullOrWhiteSpace(value) || value == "すべて" || value.Equals("all", StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().Replace(" ", "", StringComparison.Ordinal).Replace("　", "", StringComparison.Ordinal).ToUpperInvariant();
    }
}
