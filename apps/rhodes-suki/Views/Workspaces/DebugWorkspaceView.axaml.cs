using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RhodesSuki.ViewModels;

namespace RhodesSuki.Views.Workspaces;

public partial class DebugWorkspaceView : UserControl
{
    public DebugWorkspaceView()
    {
        InitializeComponent();
    }

    private async void ImportBugReportZipClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;

        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "バグ報告ZIPを選択",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("RHODES bug report ZIP")
                {
                    Patterns = ["*.zip"],
                },
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            await viewModel.ImportBugReportPathAsync(path);
    }

    private async void ImportBugReportFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "展開済みバグ報告フォルダを選択",
            AllowMultiple = false,
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            await viewModel.ImportBugReportPathAsync(path);
    }
}
