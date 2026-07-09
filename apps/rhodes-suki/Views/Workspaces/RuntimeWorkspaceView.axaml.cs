using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RhodesSuki.ViewModels;

namespace RhodesSuki.Views.Workspaces;

public partial class RuntimeWorkspaceView : UserControl
{
    public RuntimeWorkspaceView()
    {
        InitializeComponent();
    }

    private async void BrowseAdbPathClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "ADB実行ファイルを選択",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ADB executable")
                {
                    Patterns = OperatingSystem.IsWindows() ? ["adb.exe", "*.exe"] : ["adb", "*"],
                },
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            viewModel.SetManualAdbPath(path);
    }

    private async void CopyAdbDiagnosticsClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(viewModel.AdbDiagnosticCopyText);
        viewModel.MarkAdbDiagnosticsCopied();
    }
}
