using MaaFramework.Binding;

namespace RhodesSuki.Models;

public sealed record SukiMaaRuntimeSettings(
    int SchemaVersion = 1,
    string InferenceProviderId = SukiMaaInferenceCatalog.DefaultId,
    int InferenceDeviceId = 0,
    string PreferredWindowTitle = "アークナイツ",
    string PreferredWindowClass = "",
    string Win32ScreencapMethodId = SukiWin32ScreencapCatalog.DefaultId)
{
    public const int CurrentSchemaVersion = 1;
}

public sealed record SukiMaaInferenceOption(
    string Id,
    string Label,
    string Detail,
    InferenceExecutionProvider Value)
{
    public string DisplayName => Label;
}

public static class SukiMaaInferenceCatalog
{
    public const string DefaultId = "auto";

    private static readonly SukiMaaInferenceOption[] BuiltInOptions =
    [
        new(
            DefaultId,
            "自動（推奨）",
            "MAAFrameworkに推論先の選択を任せます。既存設定と同じで、DirectMLが合わない環境でもCPUで動作できます。",
            InferenceExecutionProvider.Auto),
        new(
            "cpu",
            "CPU固定",
            "互換性と再現性を優先します。GPU比較で結果が一致しない場合はこちらを使用してください。",
            InferenceExecutionProvider.CPU),
        new(
            "directml",
            "GPU（DirectML）",
            "WindowsのDXGI adapter indexでGPUを指定します。保存FrameでCPUと結果を比較してから使用してください。",
            InferenceExecutionProvider.DirectML),
    ];

    public static IReadOnlyList<SukiMaaInferenceOption> Options => BuiltInOptions;

    public static string Normalize(string? id)
    {
        var normalized = string.IsNullOrWhiteSpace(id)
            ? DefaultId
            : id.Trim().ToLowerInvariant();
        normalized = normalized switch
        {
            "default" or "automatic" => DefaultId,
            "gpu" or "dml" or "direct-ml" => "directml",
            _ => normalized,
        };
        return BuiltInOptions.Any(option => option.Id == normalized) ? normalized : DefaultId;
    }

    public static SukiMaaInferenceOption Find(string? id)
    {
        var normalized = Normalize(id);
        return BuiltInOptions.First(option => option.Id == normalized);
    }
}

public sealed record SukiWin32ScreencapOption(
    string Id,
    string Label,
    string Detail,
    Win32ScreencapMethods Value)
{
    public string DisplayName => Label;
}

public static class SukiWin32ScreencapCatalog
{
    public const string DefaultId = "background";

    private static readonly SukiWin32ScreencapOption[] BuiltInOptions =
    [
        new(
            DefaultId,
            "背景撮影（推奨）",
            "FramePoolとPrintWindowを候補にして、ウィンドウを前面へ出さずに撮影できる方式を選びます。",
            Win32ScreencapMethods.Background),
        new(
            "frame-pool",
            "FramePool",
            "MAA中国版PC設定の既定方式です。DirectX系ウィンドウを背景撮影できる可能性があります。",
            Win32ScreencapMethods.FramePool),
        new(
            "print-window",
            "PrintWindow",
            "WindowsのPrintWindowで背景撮影します。FramePoolが黒画面になる環境向けです。",
            Win32ScreencapMethods.PrintWindow),
        new(
            "dxgi-window",
            "DXGI Window",
            "画面上に表示されている対象ウィンドウをDXGIで撮影します。最小化中の撮影には向きません。",
            Win32ScreencapMethods.DXGI_DesktopDup_Window),
        new(
            "screen-dc",
            "ScreenDC",
            "Windowsの画面DCから対象ウィンドウ領域を撮影します。対象が画面上で隠れている場合や最小化中には向きません。",
            Win32ScreencapMethods.ScreenDC),
    ];

    public static IReadOnlyList<SukiWin32ScreencapOption> Options => BuiltInOptions;

    public static string Normalize(string? id)
    {
        var normalized = string.IsNullOrWhiteSpace(id)
            ? DefaultId
            : id.Trim().ToLowerInvariant();
        normalized = normalized switch
        {
            "auto" or "background-auto" => DefaultId,
            "framepool" => "frame-pool",
            "printwindow" => "print-window",
            "desktop-dup-window" or "dxgi" => "dxgi-window",
            "screendc" or "screen" => "screen-dc",
            _ => normalized,
        };
        return BuiltInOptions.Any(option => option.Id == normalized) ? normalized : DefaultId;
    }

    public static SukiWin32ScreencapOption Find(string? id)
    {
        var normalized = Normalize(id);
        return BuiltInOptions.First(option => option.Id == normalized);
    }
}

public sealed record SukiDesktopWindowPreview(
    IntPtr Handle,
    string Title,
    string ClassName,
    bool IsKnownArknightsTitle)
{
    public string DisplayName => $"{Title} / {ClassName} / 0x{Handle.ToInt64():X}";
}

public sealed record MaaPcConnectionPlan(
    Win32ScreencapMethods ScreencapMethod,
    Win32InputMethod MouseMethod,
    Win32InputMethod KeyboardMethod,
    int TargetWidth,
    int TargetHeight);
