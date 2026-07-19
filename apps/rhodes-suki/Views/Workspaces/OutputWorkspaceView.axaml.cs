using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
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
