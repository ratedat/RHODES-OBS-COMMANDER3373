using System.Globalization;

namespace RhodesSuki.Services;

public static class RhodesOperatorTaxonomy
{
    private static readonly StringComparer JapaneseComparer = StringComparer.Create(CultureInfo.GetCultureInfo("ja-JP"), false);

    private static readonly IReadOnlyList<string> ClassOrder =
    [
        "先鋒",
        "前衛",
        "重装",
        "狙撃",
        "術師",
        "医療",
        "補助",
        "特殊",
    ];

    private static readonly IReadOnlyList<(string ClassName, IReadOnlyList<string> Branches)> BranchOrderByClass =
    [
        ("先鋒", ["先駆兵", "突撃兵", "戦術家", "旗手", "偵察兵", "策士"]),
        ("前衛", ["強襲者", "闘士", "術戦士", "教官", "領主", "剣豪", "武者", "勇士", "鎌撃士", "解放者", "重剣士", "槌撃士", "本源戦士", "傭兵"]),
        ("重装", ["重盾衛士", "庇護衛士", "破壊者", "術技衛士", "決闘者", "堅城砲手", "哨戒衛士", "本源衛士"]),
        ("狙撃", ["速射手", "精密射手", "榴弾射手", "戦術射手", "散弾射手", "破城射手", "投擲手", "狩人", "旋輪射手", "翔空射手"]),
        ("術師", ["中堅術師", "拡散術師", "操機術師", "法陣術師", "秘術師", "連鎖術師", "爆撃術師", "本源術師", "創霊術師"]),
        ("医療", ["医師", "群癒師", "療養師", "放浪医", "呪癒師", "連鎖癒師", "守望者"]),
        ("補助", ["緩速師", "呪詛師", "吟遊者", "祈祷師", "召喚師", "工匠", "祭儀師"]),
        ("特殊", ["執行者", "推撃手", "潜伏者", "鉤縄師", "鬼才", "行商人", "罠師", "傀儡師", "錬金士", "巡空者"]),
    ];

    private static readonly IReadOnlyDictionary<string, int> ClassRanks =
        ClassOrder.Select((name, index) => (name, index)).ToDictionary(item => item.name, item => item.index);

    private static readonly IReadOnlyDictionary<string, (int ClassRank, int BranchRank)> BranchRanks =
        BranchOrderByClass
            .SelectMany(group => group.Branches.Select((branch, index) => (
                Branch: branch,
                Rank: (ClassRank: ClassRank(group.ClassName), BranchRank: index))))
            .ToDictionary(item => item.Branch, item => item.Rank);

    public static IReadOnlyList<string> SortClasses(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, OperatorClassComparer.Instance)
            .ToArray();
    }

    public static IReadOnlyList<string> SortBranches(IEnumerable<(string Branch, string OperatorClass)> values)
    {
        var branchClasses = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (branch, operatorClass) in values)
        {
            if (string.IsNullOrWhiteSpace(branch) || branchClasses.ContainsKey(branch))
                continue;

            branchClasses[branch] = operatorClass;
        }

        return branchClasses.Keys
            .OrderBy(value => value, new OperatorBranchComparer(branchClasses))
            .ToArray();
    }

    public static int CompareClass(string? left, string? right)
    {
        var classDelta = ClassRank(left) - ClassRank(right);
        return classDelta != 0 ? classDelta : JapaneseComparer.Compare(left ?? "", right ?? "");
    }

    public static int CompareBranch(string? left, string? right, IReadOnlyDictionary<string, string>? branchClasses = null)
    {
        var leftKey = BranchRank(left, branchClasses);
        var rightKey = BranchRank(right, branchClasses);
        var classDelta = leftKey.ClassRank - rightKey.ClassRank;
        if (classDelta != 0)
            return classDelta;

        var branchDelta = leftKey.BranchRank - rightKey.BranchRank;
        return branchDelta != 0 ? branchDelta : JapaneseComparer.Compare(left ?? "", right ?? "");
    }

    private static int ClassRank(string? value)
    {
        return ClassRanks.TryGetValue(NormalizeClass(value), out var rank) ? rank : int.MaxValue;
    }

    private static (int ClassRank, int BranchRank) BranchRank(string? value, IReadOnlyDictionary<string, string>? branchClasses)
    {
        var text = value ?? "";
        if (BranchRanks.TryGetValue(text, out var rank))
            return rank;

        return (ClassRank(branchClasses != null && branchClasses.TryGetValue(text, out var operatorClass) ? operatorClass : ""), int.MaxValue);
    }

    private static string NormalizeClass(string? value)
    {
        return value == "術士" ? "術師" : value ?? "";
    }

    private sealed class OperatorClassComparer : IComparer<string>
    {
        public static readonly OperatorClassComparer Instance = new();

        public int Compare(string? x, string? y)
        {
            return CompareClass(x, y);
        }
    }

    private sealed class OperatorBranchComparer(IReadOnlyDictionary<string, string> branchClasses) : IComparer<string>
    {
        public int Compare(string? x, string? y)
        {
            return CompareBranch(x, y, branchClasses);
        }
    }
}
