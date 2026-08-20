using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesOutputPartRegistry
{
    private static readonly SukiOutputPartDescriptor[] Items =
    [
        new(
            "status",
            "ラン状態",
            "run.base",
            "源石錐、等級、分隊など現在のラン状態",
            true,
            false,
            false,
            1200,
            120),
        new(
            "relics",
            "秘宝一覧",
            "choices.relics",
            "所持秘宝と表示除外を反映",
            true,
            true,
            true,
            1320,
            190),
        new(
            "operators",
            "招集オペレーター",
            "choices.operators",
            "選択中オペレーターをOBSへ表示",
            true,
            false,
            true,
            420,
            620),
        new(
            "effects",
            "発動効果",
            "choices.relics",
            "選択中の秘宝から発動効果を表示",
            true,
            true,
            true,
            520,
            320),
        new(
            "bosses",
            "ボスフラグ",
            "run.base",
            "現在ランのボス選択を表示",
            true,
            true,
            false,
            760,
            220),
        new(
            "special",
            "IS固有値",
            "run.special",
            "思案、啓示、灯火などキャンペーン別の値",
            true,
            true,
            false,
            600,
            180),
        new(
            "tournament",
            "大会情報",
            "run.base",
            "点数、引き出し数、配信用メモを独立表示",
            false,
            false,
            false,
            760,
            130),
    ];

    public static IReadOnlyList<SukiOutputPartDescriptor> Descriptors => Items;

    public static IReadOnlyList<SukiOutputPartPreview> BuildDefaultPreviews()
    {
        return Items.Select(item => item.ToPreview()).ToArray();
    }

    public static IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var knownSurfaceIds = RhodesProductSurfaceRegistry.Items
            .Select(item => item.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var item in Items)
        {
            if (string.IsNullOrWhiteSpace(item.Id))
                errors.Add("output part id is blank");
            else if (!ids.Add(item.Id))
                errors.Add($"duplicate output part id: {item.Id}");

            if (string.IsNullOrWhiteSpace(item.Label))
                errors.Add($"{item.Id}: label is blank");
            if (string.IsNullOrWhiteSpace(item.BindingPath))
                errors.Add($"{item.Id}: binding path is blank");
            else if (!knownSurfaceIds.Contains(item.BindingPath))
                errors.Add($"{item.Id}: binding path has no product surface: {item.BindingPath}");
            if (item.DefaultWidth <= 0)
                errors.Add($"{item.Id}: default width must be positive");
            if (item.DefaultHeight <= 0)
                errors.Add($"{item.Id}: default height must be positive");
        }

        return errors;
    }
}
