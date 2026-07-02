using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesChoiceCatalogRegistry
{
    private static readonly SukiChoiceCatalogDescriptor OperatorDescriptor = new(
        "operators",
        "operator",
        "オペレーター",
        false,
        "招集",
        "");

    private static readonly SukiChoiceCatalogDescriptor RelicDescriptor = new(
        "relics",
        "relic",
        "秘宝",
        true,
        "所持",
        "IS内");

    public static IReadOnlyList<SukiChoiceCatalogDescriptor> Descriptors { get; } =
    [
        OperatorDescriptor,
        RelicDescriptor
    ];

    public static SukiChoiceCatalogDescriptor DescriptorForKind(string kind)
    {
        return NormalizeKind(kind) switch
        {
            "relic" => RelicDescriptor,
            _ => OperatorDescriptor
        };
    }

    public static SukiChoiceCatalogView BuildView(
        string kind,
        IEnumerable<SukiChoiceItem> items,
        SukiChoiceCatalogFilterState filterState)
    {
        var descriptor = DescriptorForKind(kind);
        var source = items as IReadOnlyList<SukiChoiceItem> ?? items.ToArray();
        var filtered = RhodesChoiceFilter.Apply(source, BuildFilterOptions(descriptor, filterState));
        return new SukiChoiceCatalogView(
            descriptor,
            filterState,
            filtered,
            RhodesChoiceRows.Build(filtered, filterState.PaneColumns),
            BuildSummary(descriptor, source, filtered, filterState));
    }

    public static IReadOnlyList<SukiChoiceRow> BuildRows(IEnumerable<SukiChoiceItem> items, SukiChoiceCatalogFilterState filterState)
    {
        return RhodesChoiceRows.Build(items, filterState.PaneColumns);
    }

    public static string BuildSummary(
        string kind,
        IEnumerable<SukiChoiceItem> allItems,
        IEnumerable<SukiChoiceItem> filteredItems,
        SukiChoiceCatalogFilterState filterState)
    {
        return BuildSummary(DescriptorForKind(kind), allItems.ToArray(), filteredItems.ToArray(), filterState);
    }

    public static bool RequiresFullRefreshAfterSelectionMutation(SukiChoiceCatalogFilterState filterState)
    {
        return RhodesChoiceFilter.RequiresFullRefreshAfterSelectionMutation(ToFilterOptions(filterState));
    }

    public static bool RequiresFullRefreshAfterExclusionMutation(SukiChoiceCatalogFilterState filterState)
    {
        return RhodesChoiceFilter.RequiresFullRefreshAfterExclusionMutation(ToFilterOptions(filterState));
    }

    public static SukiChoiceFilterOptions ToFilterOptions(SukiChoiceCatalogFilterState filterState)
    {
        return new SukiChoiceFilterOptions(
            SearchText: filterState.SearchText,
            Category: filterState.Category,
            OperatorClass: filterState.OperatorClass,
            OperatorBranch: filterState.OperatorBranch,
            Rarity: filterState.Rarity,
            CampaignId: filterState.CampaignId,
            ShowSelectedFirst: filterState.ShowSelectedFirst,
            HideExcluded: filterState.HideExcluded,
            SelectedOnly: filterState.SelectedOnly);
    }

    private static SukiChoiceFilterOptions BuildFilterOptions(
        SukiChoiceCatalogDescriptor descriptor,
        SukiChoiceCatalogFilterState filterState)
    {
        var options = ToFilterOptions(filterState);
        return descriptor.Kind switch
        {
            "relic" => options with
            {
                OperatorClass = "",
                OperatorBranch = "",
                Rarity = ""
            },
            _ => options with
            {
                Category = "",
                CampaignId = ""
            }
        };
    }

    private static string BuildSummary(
        SukiChoiceCatalogDescriptor descriptor,
        IReadOnlyList<SukiChoiceItem> allItems,
        IReadOnlyList<SukiChoiceItem> filteredItems,
        SukiChoiceCatalogFilterState filterState)
    {
        if (!descriptor.IsCampaignScoped)
        {
            var selected = allItems.Count(item => item.IsSelected);
            return $"{filteredItems.Count}件 / {descriptor.SelectedSummaryLabel}{selected}名";
        }

        var scoped = (string.IsNullOrWhiteSpace(filterState.CampaignId)
            ? allItems
            : allItems.Where(item => string.Equals(item.CampaignId, filterState.CampaignId, StringComparison.Ordinal))).ToArray();
        var scopedSelected = scoped.Count(item => item.IsSelected);
        return $"{filteredItems.Count}件 / {descriptor.SelectedSummaryLabel}{scopedSelected}件 / {descriptor.TotalSummaryLabel}{scoped.Length}件";
    }

    private static string NormalizeKind(string kind)
    {
        return kind.Trim().ToLowerInvariant() switch
        {
            "relics" => "relic",
            "relic" => "relic",
            _ => "operator"
        };
    }
}
