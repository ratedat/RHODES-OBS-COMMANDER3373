using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesRunFieldRegistry
{
    private static readonly RunFieldDescriptor IngotDescriptor =
        new(
            "ingot",
            "源石錐",
            "run.ingot",
            "run.ingot",
            "MAA-OCR / Template anchor",
            0,
            0,
            state => state.Ingot.ToString(),
            _ => "右上の源石錐アイコンを基準に取得");

    private static readonly RunFieldDescriptor SpecialDescriptor =
        new(
            "special",
            "IS特殊値",
            "run.special",
            "run.special",
            "MAA-OCR / campaign-specific",
            1,
            3,
            BuildSpecialStatusValue,
            BuildSpecialStatusDetail);

    private static readonly RunFieldDescriptor DifficultyDescriptor =
        new(
            "difficulty",
            "等級",
            "run.difficulty",
            "run.difficulty_grade",
            "MAA-OCR / squad panel",
            2,
            1,
            state => ValueOrDash(state.Difficulty),
            _ => "分隊情報パネルから確定");

    private static readonly RunFieldDescriptor SquadDescriptor =
        new(
            "squad",
            "分隊",
            "run.squad",
            "run.squad_name",
            "MAA-OCR",
            3,
            2,
            state => ValueOrDash(state.Squad),
            _ => "分隊カードまたは情報パネル");

    private static readonly RunFieldDescriptor[] Descriptors =
    [
        IngotDescriptor,
        SpecialDescriptor,
        DifficultyDescriptor,
        SquadDescriptor
    ];

    public static IReadOnlyList<SukiStatusChip> BuildHeaderStatusChips(SukiRunStateSnapshot state)
    {
        return DescriptorsFor(state)
            .OrderBy(item => item.HeaderOrder)
            .Select(item => new SukiStatusChip(item.Label, item.Value(state), item.HeaderDetailId))
            .ToArray();
    }

    public static IReadOnlyList<SukiRunFieldPreview> BuildRunFieldPreviews(SukiRunStateSnapshot state)
    {
        return DescriptorsFor(state)
            .OrderBy(item => item.PreviewOrder)
            .Select(item => new SukiRunFieldPreview(
                item.Label,
                item.Value(state),
                item.Source,
                item.RecognitionTaskId,
                item.Detail(state)))
            .ToArray();
    }

    private static IReadOnlyList<RunFieldDescriptor> DescriptorsFor(SukiRunStateSnapshot state)
    {
        if (!string.Equals(state.CampaignId, "is3_mizuki", StringComparison.Ordinal))
            return Descriptors;

        return
        [
            IngotDescriptor,
            BuildMizukiSpecialDescriptor("key", "鍵", 1, 3),
            BuildMizukiSpecialDescriptor("light", "灯火", 2, 4),
            BuildMizukiSpecialDescriptor("rejectionReaction", "拒絶反応", 3, 5),
            DifficultyDescriptor with { HeaderOrder = 4 },
            SquadDescriptor with { HeaderOrder = 5 }
        ];
    }

    private static RunFieldDescriptor BuildMizukiSpecialDescriptor(
        string fieldId,
        string label,
        int headerOrder,
        int previewOrder)
    {
        return new RunFieldDescriptor(
            fieldId,
            label,
            "run.special",
            $"run.special.{fieldId}",
            "MAA-OCR / campaign-specific",
            headerOrder,
            previewOrder,
            state => BuildMizukiSpecialValue(state, fieldId),
            state => CurrentCampaignSpecialFields(state)
                .FirstOrDefault(field => string.Equals(field.FieldId, fieldId, StringComparison.Ordinal))
                ?.Detail ?? "取得値なし");
    }

    private static string BuildMizukiSpecialValue(SukiRunStateSnapshot state, string fieldId)
    {
        var field = CurrentCampaignSpecialFields(state)
            .FirstOrDefault(item => string.Equals(item.FieldId, fieldId, StringComparison.Ordinal));
        if (field is null)
            return "-";

        if (string.Equals(fieldId, "rejectionReaction", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(field.Detail)
            && !field.Detail.Equals("取得値なし", StringComparison.Ordinal))
        {
            return field.Detail.Trim();
        }

        return ValueOrDash(field.Value.Equals("未入力", StringComparison.Ordinal) ? "" : field.Value);
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
