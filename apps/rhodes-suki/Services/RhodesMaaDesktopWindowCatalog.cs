using MaaFramework.Binding;
using RhodesSuki.Models;

namespace RhodesSuki.Services;

public static class RhodesMaaDesktopWindowCatalog
{
    private const string ArknightsClientWindowClass = "UnityWndClass";

    private static readonly HashSet<string> KnownArknightsTitles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "アークナイツ",
            "Arknights",
            "明日方舟",
            "명일방주",
        };

    public static IReadOnlyList<SukiDesktopWindowPreview> Discover(
        string? preferredTitle,
        string? preferredClass)
    {
        RhodesMaaSession.PrepareNativeRuntime();
        using var windows = MaaToolkit.Shared.Desktop.Window.Find();
        return Rank(windows, preferredTitle, preferredClass);
    }

    public static IReadOnlyList<SukiDesktopWindowPreview> Rank(
        IEnumerable<DesktopWindowInfo> windows,
        string? preferredTitle,
        string? preferredClass)
    {
        ArgumentNullException.ThrowIfNull(windows);
        var title = preferredTitle?.Trim() ?? "";
        var className = preferredClass?.Trim() ?? "";

        return windows
            .Where(window => window.Handle != IntPtr.Zero && !string.IsNullOrWhiteSpace(window.Name))
            .Select(window => new SukiDesktopWindowPreview(
                window.Handle,
                window.Name.Trim(),
                window.ClassName?.Trim() ?? "",
                KnownArknightsTitles.Contains(window.Name.Trim())))
            .OrderBy(window => RankWindow(window, title, className))
            .ThenBy(window => window.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(window => window.Handle.ToInt64())
            .ToArray();
    }

    private static int RankWindow(
        SukiDesktopWindowPreview window,
        string preferredTitle,
        string preferredClass)
    {
        var titleMatches = !string.IsNullOrWhiteSpace(preferredTitle)
            && window.Title.Equals(preferredTitle, StringComparison.OrdinalIgnoreCase);
        var classMatches = !string.IsNullOrWhiteSpace(preferredClass)
            && window.ClassName.Equals(preferredClass, StringComparison.OrdinalIgnoreCase);
        var defaultClientClassMatches = string.IsNullOrWhiteSpace(preferredClass)
            && window.IsKnownArknightsTitle
            && window.ClassName.Equals(ArknightsClientWindowClass, StringComparison.OrdinalIgnoreCase);
        if (titleMatches && (classMatches || defaultClientClassMatches))
            return 0;
        if (titleMatches)
            return 1;
        if (window.IsKnownArknightsTitle && (classMatches || defaultClientClassMatches))
            return 2;
        if (window.IsKnownArknightsTitle)
            return 3;
        if (classMatches)
            return 4;
        return 5;
    }
}

public static class RhodesMaaPcConnectionPolicy
{
    public static MaaPcConnectionPlan Resolve(
        string? screencapMethodId,
        string? mouseMethodId,
        string? keyboardMethodId)
    {
        var screencap = SukiWin32ScreencapCatalog.Find(screencapMethodId);
        var mouse = SukiWin32InputCatalog.FindMouse(mouseMethodId);
        var keyboard = SukiWin32InputCatalog.FindKeyboard(keyboardMethodId);
        return new MaaPcConnectionPlan(
            screencap.Value,
            mouse.Value,
            keyboard.Value,
            1280,
            720);
    }
}
