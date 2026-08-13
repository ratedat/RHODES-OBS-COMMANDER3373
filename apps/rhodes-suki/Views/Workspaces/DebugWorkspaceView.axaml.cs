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

    private async void SelectMaaMaterialsFileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "MAA解析素材を選択",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MAAログ・証跡") { Patterns = ["*.log", "*.txt", "*.json", "*.zip"] },
                FilePickerFileTypes.All,
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            viewModel.MaaDevelopmentTools.SetMaterialsPath(path);
    }

    private async void SelectMaaMaterialsFolderClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;
        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "MAA解析素材フォルダを選択",
            AllowMultiple = false,
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            viewModel.MaaDevelopmentTools.SetMaterialsPath(path);
    }

    private async void SelectMaaLogAnalyzerClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "MaaLogAnalyzerのローカル実行ファイルを選択",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Windows executable") { Patterns = ["*.exe"] }],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            viewModel.MaaDevelopmentTools.SetLogAnalyzerPath(path);
    }

    private async void SelectRecognitionLabImageClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
            return;
        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider is null)
            return;
        var files = await storageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "認識ラボのFrame画像を選択",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Frame image") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.webp", "*.bmp"] },
            ],
        });
        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            viewModel.RecognitionLab.SetImagePath(path);
    }
}
