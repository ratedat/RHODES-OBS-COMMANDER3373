using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesOutputPartRegistry
{
    private static readonly SukiOutputPartDescriptor[] Items =
    [
        new(
            "operators",
            "招集オペレーター",
            "choices.operators",
            "選択中オペレーターをOBSへ表示",
            true,
            false,
            true,
            420,
            132),
        new(
            "relics",
            "秘宝一覧",
            "choices.relics",
            "所持秘宝と表示除外を反映",
            true,
            true,
            true,
            420,
            170),
        new(
            "run",
            "ラン取得値",
            "run.base",
            "源石錐、等級、分隊、IS特殊値",
            true,
            false,
            false,
            260,
            116),
        new(
            "special",
            "IS固有値",
            "run.special",
            "思案、啓示、灯火などキャンペーン別の値",
            true,
            true,
            false,
            300,
            126),
        new(
            "recognition",
            "認識ステータス",
            "recognition.candidates",
            "デバッグ配布時のみ候補/信頼度を表示",
            false,
            true,
            false,
            360,
            92),
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
