using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesWorkspaceRegistry
{
    private static readonly SukiWorkspaceNavItem[] WorkspaceItems =
    [
        new("run", "ラン", "RUN", "取得値とIS固有値"),
        new("choices", "選択", "CHOICES", "オペレーターと秘宝"),
        new("recognition", "認識", "RECOGNITION", "OCR/テンプレート候補"),
        new("output", "出力", "OUTPUT", "OBS表示構成"),
        new("runtime", "ランタイム", "RUNTIME", "ADB/MAA/GLM/Ollama"),
        new("debug", "デバッグ", "DEBUG", "ログと検証情報"),
    ];

    private static readonly Dictionary<string, SukiWorkspaceNavItem> WorkspaceById = WorkspaceItems
        .ToDictionary(item => item.Id, StringComparer.Ordinal);

    private static readonly IReadOnlyDictionary<string, string> TitlesById = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["run"] = "ラン取得値",
        ["choices"] = "選択カタログ",
        ["recognition"] = "認識ワークフロー",
        ["output"] = "出力 / OBS",
        ["runtime"] = "ランタイム",
        ["debug"] = "デバッグ",
    };

    public static IReadOnlyList<SukiWorkspaceNavItem> Items => WorkspaceItems;

    public static string Normalize(string? workspaceId)
    {
        var id = string.IsNullOrWhiteSpace(workspaceId) ? "" : workspaceId.Trim();
        return IsKnown(id) ? id : "run";
    }

    public static bool IsKnown(string? workspaceId)
    {
        return !string.IsNullOrWhiteSpace(workspaceId)
            && WorkspaceById.ContainsKey(workspaceId.Trim());
    }

    public static string TitleFor(string? workspaceId)
    {
        return TitlesById.TryGetValue(Normalize(workspaceId), out var title)
            ? title
            : TitlesById["run"];
    }
}
