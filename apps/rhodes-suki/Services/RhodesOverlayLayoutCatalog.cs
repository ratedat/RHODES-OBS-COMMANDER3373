using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesOverlayLayoutCatalog
{
    public const int CanvasWidth = 1920;
    public const int CanvasHeight = 1080;
    public const int MinimumWidth = 160;
    public const int MinimumHeight = 80;

    private static readonly SukiOverlayLayoutState[] Defaults =
    [
        new("status", true, 40, 36, 1200, 120, 1),
        new("relics", true, 40, 850, 1320, 190, 6),
        new("operators", true, 1460, 260, 420, 620, 5),
        new("effects", true, 40, 420, 520, 320, 3),
        new("bosses", true, 600, 420, 760, 220, 4),
        new("special", true, 1280, 36, 600, 180, 2),
    ];

    public static IReadOnlyList<SukiOverlayLayoutState> BuildDefaultStates()
    {
        return Defaults.Select(item => item with { }).ToArray();
    }

    public static IReadOnlyList<SukiOverlayLayoutPreview> BuildPreviews(
        IEnumerable<SukiOverlayLayoutState>? states = null)
    {
        return Normalize(states).Select(state => new SukiOverlayLayoutPreview(LabelFor(state.Id), state)).ToArray();
    }

    public static IReadOnlyList<SukiOverlayLayoutState> Normalize(
        IEnumerable<SukiOverlayLayoutState>? states)
    {
        var supplied = (states ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .GroupBy(item => item.Id.Trim().ToLowerInvariant(), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.Ordinal);

        return Defaults.Select((fallback, index) =>
        {
            var source = supplied.TryGetValue(fallback.Id, out var candidate) ? candidate : fallback;
            var width = Math.Clamp(source.Width, MinimumWidth, CanvasWidth);
            var height = Math.Clamp(source.Height, MinimumHeight, CanvasHeight);
            return new SukiOverlayLayoutState(
                fallback.Id,
                source.Enabled,
                Math.Clamp(source.X, 0, CanvasWidth - width),
                Math.Clamp(source.Y, 0, CanvasHeight - height),
                width,
                height,
                Math.Clamp(source.ZIndex, 1, Defaults.Length));
        }).ToArray();
    }

    public static IReadOnlyList<string> Validate(IEnumerable<SukiOverlayLayoutState> states)
    {
        var errors = new List<string>();
        var items = states.ToArray();
        var knownIds = Defaults.Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);

        foreach (var item in items)
        {
            if (!knownIds.Contains(item.Id))
                errors.Add($"unknown overlay layout part: {item.Id}");
            else if (!ids.Add(item.Id))
                errors.Add($"duplicate overlay layout part: {item.Id}");

            if (item.Width < MinimumWidth || item.Height < MinimumHeight)
                errors.Add($"{item.Id}: overlay layout size is below the minimum");
            if (item.X < 0 || item.Y < 0 || item.X + item.Width > CanvasWidth || item.Y + item.Height > CanvasHeight)
                errors.Add($"{item.Id}: overlay layout exceeds the 1920x1080 canvas");
            if (item.ZIndex < 1 || item.ZIndex > Defaults.Length)
                errors.Add($"{item.Id}: overlay layout z-index is out of range");
        }

        foreach (var missing in knownIds.Except(ids))
            errors.Add($"missing overlay layout part: {missing}");

        return errors;
    }

    private static string LabelFor(string id)
    {
        return id switch
        {
            "status" => "ラン状態",
            "relics" => "秘宝",
            "operators" => "招集オペレーター",
            "effects" => "発動効果",
            "bosses" => "ボス",
            "special" => "特殊値",
            _ => id,
        };
    }
}
