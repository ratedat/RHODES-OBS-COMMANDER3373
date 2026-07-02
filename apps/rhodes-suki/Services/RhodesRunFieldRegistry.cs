using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRunFieldRegistry
{
    private static readonly RunFieldDescriptor[] Descriptors =
    [
        new(
            "ingot",
            "源石錐",
            "run.ingot",
            "run.ingot",
            "MAA-OCR / Template anchor",
            0,
            0,
            state => state.Ingot.ToString(),
            _ => "右上の源石錐アイコンを基準に取得"),
        new(
            "special",
            "IS特殊値",
            "run.special",
            "run.special",
            "MAA-OCR / campaign-specific",
            1,
            3,
            BuildSpecialStatusValue,
            BuildSpecialStatusDetail),
        new(
            "difficulty",
            "等級",
            "run.difficulty",
            "run.difficulty_grade",
            "MAA-OCR / squad panel",
            2,
            1,
            state => ValueOrDash(state.Difficulty),
            _ => "分隊情報パネルから確定"),
        new(
            "squad",
            "分隊",
            "run.squad",
            "run.squad_name",
            "MAA-OCR",
            3,
            2,
            state => ValueOrDash(state.Squad),
            _ => "分隊カードまたは情報パネル")
    ];

    public static IReadOnlyList<SukiStatusChip> BuildHeaderStatusChips(SukiRunStateSnapshot state)
    {
        return Descriptors
            .OrderBy(item => item.HeaderOrder)
            .Select(item => new SukiStatusChip(item.Label, item.Value(state), item.HeaderDetailId))
            .ToArray();
    }

    public static IReadOnlyList<SukiRunFieldPreview> BuildRunFieldPreviews(SukiRunStateSnapshot state)
    {
        return Descriptors
            .OrderBy(item => item.PreviewOrder)
            .Select(item => new SukiRunFieldPreview(
                item.Label,
                item.Value(state),
                item.Source,
                item.RecognitionTaskId,
                item.Detail(state)))
            .ToArray();
    }

    public static string BuildSpecialStatusValue(SukiRunStateSnapshot state)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(state.SquadRandomEffect))
            parts.Add($"分隊効果={state.SquadRandomEffect.Trim()}");

        foreach (var field in CurrentCampaignSpecialFields(state))
        {
            var value = field.Value.Trim();
            if (string.IsNullOrWhiteSpace(value) || value.Equals("未入力", StringComparison.Ordinal))
                continue;
            parts.Add($"{field.Label}={value}");
        }

        return parts.Count == 0 ? "-" : string.Join(" / ", parts);
    }

    public static string BuildSpecialStatusDetail(SukiRunStateSnapshot state)
    {
        var labels = CurrentCampaignSpecialFields(state)
            .Select(field => field.Label)
            .Where(label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (labels.Length == 0)
            return "このISの固有値定義を追加してください";

        return $"{string.Join("、", labels)}など、ISごとの値だけを扱う";
    }

    public static IReadOnlyList<SukiSpecialFieldState> CurrentCampaignSpecialFields(SukiRunStateSnapshot state)
    {
        return (state.SpecialFields ?? Array.Empty<SukiSpecialFieldState>())
            .Where(field => string.Equals(field.CampaignId, state.CampaignId, StringComparison.Ordinal))
            .ToArray();
    }

    private static string ValueOrDash(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
    }

    private sealed record RunFieldDescriptor(
        string Id,
        string Label,
        string HeaderDetailId,
        string RecognitionTaskId,
        string Source,
        int HeaderOrder,
        int PreviewOrder,
        Func<SukiRunStateSnapshot, string> Value,
        Func<SukiRunStateSnapshot, string> Detail);
}
