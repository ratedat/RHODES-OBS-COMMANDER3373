using Avalonia;
using RhodesSuki.Services;

namespace RhodesSuki;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        RhodesRunStateStore.PrepareForStartupAsync().GetAwaiter().GetResult();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
