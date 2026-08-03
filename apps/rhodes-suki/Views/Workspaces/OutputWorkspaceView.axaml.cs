using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using RhodesSuki.Models;
using RhodesSuki.ViewModels;

namespace RhodesSuki.Views.Workspaces;

public partial class OutputWorkspaceView : UserControl
{
    public OutputWorkspaceView()
    {
        InitializeComponent();
    }

    private async void CopyUrlClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: string url } || string.IsNullOrWhiteSpace(url))
            return;

        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null)
            return;

        await clipboard.SetTextAsync(url);
        if (DataContext is MainWindowViewModel viewModel)
            viewModel.MarkObsUrlCopied(url);
    }

    private async void ExportOutputProfileClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null || DataContext is not MainWindowViewModel viewModel)
            return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "出力プロファイルを保存",
            SuggestedFileName = $"rhodes-output-profile-{DateTime.Now:yyyyMMdd-HHmmss}.json",
            DefaultExtension = "json",
            FileTypeChoices = new[] { JsonFileType }
        });

        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            await viewModel.ExportOutputProfileAsync(path);
    }

    private async void ImportOutputProfileClick(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null || DataContext is not MainWindowViewModel viewModel)
            return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "出力プロファイルを読み込む",
            AllowMultiple = false,
            FileTypeFilter = new[] { JsonFileType }
        });

        var path = files.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path))
            await viewModel.ImportOutputProfileAsync(path);
    }

    private static readonly FilePickerFileType JsonFileType = new("JSON")
    {
        Patterns = new[] { "*.json" },
        MimeTypes = new[] { "application/json" }
    };

    private void LayoutMoveDragDelta(object? sender, VectorEventArgs e)
    {
        if (sender is not Thumb { DataContext: SukiOverlayLayoutPreview item }
            || DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.MoveOverlayLayoutItem(item, e.Vector.X, e.Vector.Y);
    }

    private void LayoutResizeDragDelta(object? sender, VectorEventArgs e)
    {
        if (sender is not Thumb { DataContext: SukiOverlayLayoutPreview item }
            || DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.ResizeOverlayLayoutItem(item, e.Vector.X, e.Vector.Y);
    }

    private void LayoutItemPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: SukiOverlayLayoutPreview item }
            && DataContext is MainWindowViewModel viewModel)
            viewModel.SelectOverlayLayoutItem(item);
    }
}
