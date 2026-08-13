namespace RhodesSuki.Models;

public sealed record SukiAdbConnectionSettings(
    int SchemaVersion = 1,
    bool AutoDetect = true,
    bool AlwaysAutoDetect = false,
    string EmulatorRoot = "",
    string EmulatorExecutablePath = "",
    bool MuMuScreenshotEnhancementEnabled = false,
    bool MuMuTouchEnhancementEnabled = false,
    bool MuMuBridgeConnectionEnabled = false,
    int MuMuInstanceIndex = 0,
    bool LdPlayerScreenshotEnhancementEnabled = false,
    int LdPlayerInstanceIndex = 0,
    string GamePackage = SukiAdbGamePackageCatalog.DefaultPackageName,
    int GameCloneIndex = 0,
    string InputFallbackMethodId = "minitouch",
    string ScreencapFallbackMethodId = "raw-gzip",
    int ReconnectAttempts = 3,
    int ReconnectDelayMs = 1000,
    bool RestartAdbServerOnFailure = false,
    bool HardRestartAdbProcessOnFailure = false,
    bool RestartEmulatorOnFailure = false,
    bool KillAdbOnExit = false,
    bool UseManagedAdb = false,
    bool LightweightAdb = false)
{
    public const int CurrentSchemaVersion = 2;
}

public sealed record SukiAdbGamePackageOption(
    string Id,
    string Label,
    string PackageName,
    string Detail,
    bool IsCustom = false)
{
    public string DisplayName => IsCustom ? Label : $"{Label} — {PackageName}";
}

public static class SukiAdbGamePackageCatalog
{
    public const string DefaultId = "jp";
    public const string DefaultPackageName = "com.YoStarJP.Arknights";

    private static readonly IReadOnlyList<SukiAdbGamePackageOption> BuiltInOptions =
    [
        new(DefaultId, "日本版（JP）", DefaultPackageName, "日本版です。新規設定ではこの項目を使用します。"),
        new("en", "グローバル版（EN）", "com.YoStarEN.Arknights", "英語版クライアントの接続先パッケージです。"),
        new("kr", "韓国版（KR）", "com.YoStarKR.Arknights", "韓国版クライアントの接続先パッケージです。"),
        new("cn-official", "中国版（公式）", "com.hypergryph.arknights", "中国大陸の公式クライアント用パッケージです。"),
        new("cn-bilibili", "中国版（Bilibili）", "com.hypergryph.arknights.bilibili", "中国大陸のBilibiliクライアント用パッケージです。"),
        new("tw", "繁中版（TW）", "tw.txwy.and.arknights", "繁体字中国語版クライアントの接続先パッケージです。"),
        new("custom", "カスタム（一覧外のpackage名）", "", "一覧外のリージョンや将来のクライアントだけ手動入力します。", IsCustom: true),
    ];

    public static IReadOnlyList<SukiAdbGamePackageOption> Options => BuiltInOptions;

    public static SukiAdbGamePackageOption Default =>
        BuiltInOptions.First(option => option.Id == DefaultId);

    public static SukiAdbGamePackageOption FindById(string? id) =>
        BuiltInOptions.FirstOrDefault(option => string.Equals(option.Id, id?.Trim(), StringComparison.OrdinalIgnoreCase))
        ?? Default;

    public static SukiAdbGamePackageOption FindByPackage(string? packageName)
    {
        var normalized = NormalizePackage(packageName);
        return BuiltInOptions.FirstOrDefault(option =>
                   !option.IsCustom
                   && string.Equals(option.PackageName, normalized, StringComparison.OrdinalIgnoreCase))
               ?? BuiltInOptions.First(option => option.IsCustom);
    }

    public static string NormalizePackage(string? packageName) =>
        string.IsNullOrWhiteSpace(packageName) ? DefaultPackageName : packageName.Trim();
}
