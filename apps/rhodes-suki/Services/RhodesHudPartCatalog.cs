using System.ComponentModel;

namespace RhodesSuki.Services;

/// <summary>
/// HUD小窓に表示する内容の選択肢。出力ワークスペースのチェックで切り替え、suki-settingsに永続化する。
/// ラン値チップのIdは RhodesRunFieldRegistry の HeaderDetailId (run.ingot など) と一致させ、
/// HeaderStatusChips のフィルタキーとして使う。
/// </summary>
public static class RhodesHudPartCatalog
{
    private const string CatalogVersionToken = "__hud_catalog_v2";

    public const string RelicsPartId = "relics";
    public const string TierPartId = "tier";
    public const string ApplySummaryPartId = "apply";
    public const string ConnectionPartId = "connection";

    private static readonly (string Id, string Label, bool DefaultEnabled)[] Parts =
    [
        ("run.ingot", "源石錐", true),
        ("run.special", "IS特殊値", true),
        ("run.difficulty", "等級", true),
        ("run.squad", "分隊", true),
        (RelicsPartId, "秘宝", true),
        (TierPartId, "多元化珍品Tier", true),
        (ApplySummaryPartId, "直近の反映結果", true),
        (ConnectionPartId, "接続状態", false),
    ];

    public static string DefaultEnabledIds =>
        string.Join(",", Parts.Where(part => part.DefaultEnabled).Select(part => part.Id));

    public static IReadOnlyList<SukiHudPartOption> Build(string? enabledCsv, Action onToggled)
    {
        var enabled = ParseEnabledIds(enabledCsv);
        return Parts
            .Select(part => new SukiHudPartOption(part.Id, part.Label, enabled.Contains(part.Id), onToggled))
            .ToArray();
    }

    public static string SerializeEnabledIds(IEnumerable<SukiHudPartOption> options)
    {
        return string.Join(
            ",",
            options
                .Where(option => option.IsEnabled)
                .Select(option => option.Id)
                .Append(CatalogVersionToken));
    }

    private static HashSet<string> ParseEnabledIds(string? csv)
    {
        var hasExplicitConfig = !string.IsNullOrWhiteSpace(csv);
        var normalizedCsv = hasExplicitConfig ? csv! : DefaultEnabledIds;

        var enabled = normalizedCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
        var hasCatalogVersion = enabled.Remove(CatalogVersionToken);
        if (hasExplicitConfig && !hasCatalogVersion)
            enabled.Add(RelicsPartId);
        return enabled;
    }
}

/// <summary>HUD表示部品のチェックボックス1個分。切替時にコールバックでHUDへ反映される。</summary>
public sealed class SukiHudPartOption : INotifyPropertyChanged
{
    private readonly Action _onToggled;
    private bool _isEnabled;

    public SukiHudPartOption(string id, string label, bool isEnabled, Action onToggled)
    {
        Id = id;
        Label = label;
        _isEnabled = isEnabled;
        _onToggled = onToggled;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string Label { get; }

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled == value)
                return;

            _isEnabled = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            _onToggled();
        }
    }
}
