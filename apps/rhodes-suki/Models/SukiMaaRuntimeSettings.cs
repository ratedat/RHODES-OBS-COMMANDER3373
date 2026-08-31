using MaaFramework.Binding;

namespace RhodesSuki.Models;

public sealed record SukiMaaRuntimeSettings(
    int SchemaVersion = 2,
    string ConnectionTargetId = SukiMaaConnectionTargetCatalog.DefaultId,
    string InferenceProviderId = SukiMaaInferenceCatalog.DefaultId,
    int InferenceDeviceId = 0,
    string PreferredWindowTitle = "アークナイツ",
    string PreferredWindowClass = "",
    string Win32ScreencapMethodId = SukiWin32ScreencapCatalog.DefaultId,
    string Win32MouseMethodId = SukiWin32InputCatalog.DefaultMouseId,
    string Win32KeyboardMethodId = SukiWin32InputCatalog.DefaultKeyboardId)
{
    public const int CurrentSchemaVersion = 2;
}

public sealed record SukiMaaConnectionTargetOption(
    string Id,
    string Label,
    string Detail,
    bool IsPc)
{
    public string DisplayName => Label;
}

public static class SukiMaaConnectionTargetCatalog
{
    public const string DefaultId = "adb";

    private static readonly SukiMaaConnectionTargetOption[] BuiltInOptions =
    [
        new(DefaultId, "Android / エミュレーター（ADB）", "ADB接続済みのAndroid端末から取得します。従来の取得経路です。", false),
        new("pc", "PCクライアント", "起動済みのアークナイツPC版ウィンドウへ接続し、同じ認識プロファイルで取得します。", true),
    ];

    public static IReadOnlyList<SukiMaaConnectionTargetOption> Options => BuiltInOptions;

    public static string Normalize(string? id)
    {
        var normalized = id?.Trim().ToLowerInvariant() ?? "";
        normalized = normalized switch
        {
            "pc-client" or "windows" or "win32" => "pc",
            "android" or "emulator" => DefaultId,
            _ => normalized,
        };
        return BuiltInOptions.Any(option => option.Id == normalized) ? normalized : DefaultId;
    }

    public static SukiMaaConnectionTargetOption Find(string? id)
    {
        var normalized = Normalize(id);
        return BuiltInOptions.First(option => option.Id == normalized);
    }
}

public enum MaaSessionControllerKind
{
    None,
    Adb,
    Offline,
    Win32,
}

public static class SukiMaaConnectionTargetPolicy
{
    public static bool IsReady(
        string? connectionTargetId,
        bool isConnected,
        MaaSessionControllerKind controllerKind)
    {
        if (!isConnected)
            return false;

        return SukiMaaConnectionTargetCatalog.Find(connectionTargetId).IsPc
            ? controllerKind == MaaSessionControllerKind.Win32
            : controllerKind == MaaSessionControllerKind.Adb;
    }
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
    // MAA v6.16.8 uses FramePool as its default AttachWindow capture method.
    // Source: https://github.com/MaaAssistantArknights/MaaAssistantArknights/blob/v6.16.8/src/MaaWpfGui/Models/EmulatorConnectionExtra/Win32Extra.cs
    public const string DefaultId = "frame-pool";

    private static readonly SukiWin32ScreencapOption[] BuiltInOptions =
    [
        new(
            DefaultId,
            "FramePool（推奨）",
            "MAA PC設定の既定方式です。高速な背景撮影に対応し、このPCの日本版クライアントで完全な画面を確認済みです。",
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
        new(
            "background",
            "背景複合（上級）",
            "FramePoolとPrintWindowを候補にします。このPCでは成功扱いでも画面下部が欠けたため、撮影テストで完全性を確認して使用してください。",
            Win32ScreencapMethods.Background),
    ];

    public static IReadOnlyList<SukiWin32ScreencapOption> Options => BuiltInOptions;

    public static string Normalize(string? id)
    {
        var normalized = string.IsNullOrWhiteSpace(id)
            ? DefaultId
            : id.Trim().ToLowerInvariant();
        normalized = normalized switch
        {
            "auto" => DefaultId,
            "background-auto" => "background",
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

public sealed record SukiWin32InputOption(
    string Id,
    string Label,
    string Detail,
    Win32InputMethod Value)
{
    public string DisplayName => Label;
}

public static class SukiWin32InputCatalog
{
    // Keep the proven MAA AttachWindow defaults while exposing compatible MaaFramework dev methods.
    // Sources:
    // https://github.com/MaaAssistantArknights/MaaAssistantArknights/blob/v6.16.8/src/MaaWpfGui/Models/EmulatorConnectionExtra/Win32Extra.cs
    // https://github.com/MaaXYZ/MaaFramework/blob/v5.13.0-beta.5/docs/en_us/2.4-ControlMethods.md
    public const string DefaultMouseId = "send-message-cursor";
    public const string DefaultKeyboardId = "send-message";

    private static readonly SukiWin32InputOption[] BuiltInMouseOptions =
    [
        new(
            DefaultMouseId,
            "SendMsg + CursorPos（推奨）",
            "MAAの既定方式です。操作時だけマウス位置を対象座標へ移し、送信後に元へ戻します。",
            Win32InputMethod.SendMessageWithCursorPos),
        new(
            "anchored-touch",
            "AnchoredTouch（開発版）",
            "カーソルを動かさず、合成タッチでクリックとスワイプを送ります。Windows 10 1809以降向けです。スクロールホイールには対応しないため、3373の取得プロファイルのタップ／スワイプ専用です。対象ウィンドウが隠れている場合は一時的に表示状態が変わることがあります。",
            Win32InputMethod.AnchoredTouch),
        new(
            "send-message-window",
            "SendMsg + WindowPos",
            "マウスを動かさずに入力します。座標合わせのため対象ウィンドウが一時的に移動する場合があります。",
            Win32InputMethod.SendMessageWithWindowPos),
        new(
            "seize",
            "Seize（前面専用）",
            "実マウスを一時的に占有する互換方式です。ほかの方式で操作できない場合だけ使用してください。",
            Win32InputMethod.Seize),
    ];

    private static readonly SukiWin32InputOption[] BuiltInKeyboardOptions =
    [
        new(
            DefaultKeyboardId,
            "SendMessage（推奨）",
            "MAAの既定接続方式です。RHODESの取得処理はキーボード操作を発行しません。",
            Win32InputMethod.SendMessage),
        new(
            "post-message",
            "PostMessage",
            "非同期の代替接続方式です。RHODESの取得処理はキーボード操作を発行しません。",
            Win32InputMethod.PostMessage),
        new(
            "seize",
            "Seize（前面専用）",
            "互換性確認用の接続方式です。RHODESの取得処理はキーボード操作を発行しません。",
            Win32InputMethod.Seize),
    ];

    public static IReadOnlyList<SukiWin32InputOption> MouseOptions => BuiltInMouseOptions;

    public static IReadOnlyList<SukiWin32InputOption> KeyboardOptions => BuiltInKeyboardOptions;

    public static string NormalizeMouse(string? id)
    {
        var normalized = id?.Trim().ToLowerInvariant() ?? "";
        normalized = normalized switch
        {
            "sendmessagewithcursorpos" or "send-with-cursor-pos" or "cursor" => DefaultMouseId,
            "anchored" or "anchoredtouch" or "touch" => "anchored-touch",
            "sendmessagewithwindowpos" or "send-with-window-pos" or "window" => "send-message-window",
            _ => normalized,
        };
        return BuiltInMouseOptions.Any(option => option.Id == normalized) ? normalized : DefaultMouseId;
    }

    public static string NormalizeKeyboard(string? id)
    {
        var normalized = id?.Trim().ToLowerInvariant() ?? "";
        normalized = normalized switch
        {
            "sendmsg" or "sendmessage" => DefaultKeyboardId,
            "postmsg" or "postmessage" => "post-message",
            _ => normalized,
        };
        return BuiltInKeyboardOptions.Any(option => option.Id == normalized) ? normalized : DefaultKeyboardId;
    }

    public static SukiWin32InputOption FindMouse(string? id)
    {
        var normalized = NormalizeMouse(id);
        return BuiltInMouseOptions.First(option => option.Id == normalized);
    }

    public static SukiWin32InputOption FindKeyboard(string? id)
    {
        var normalized = NormalizeKeyboard(id);
        return BuiltInKeyboardOptions.First(option => option.Id == normalized);
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
