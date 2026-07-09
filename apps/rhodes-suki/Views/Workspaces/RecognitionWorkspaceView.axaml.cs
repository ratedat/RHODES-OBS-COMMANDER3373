using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RhodesSuki.Models;
using RhodesSuki.ViewModels;

namespace RhodesSuki.Views.Workspaces;

public partial class RecognitionWorkspaceView : UserControl
{
    private Control? _roiDragSource;
    private Control? _roiResizeSource;
    private IPointer? _roiActivePointer;
    private TopLevel? _escapeHandlerTopLevel;

    public RecognitionWorkspaceView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        // ROIドラッグ中のEscapeキャンセルはフォーカス位置に依存させないため、TopLevelで拾う。
        _escapeHandlerTopLevel = TopLevel.GetTopLevel(this);
        _escapeHandlerTopLevel?.AddHandler(KeyDownEvent, CancelRoiInteractionOnEscape, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _escapeHandlerTopLevel?.RemoveHandler(KeyDownEvent, CancelRoiInteractionOnEscape);
        _escapeHandlerTopLevel = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void RoiOverlayPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control
            || control.DataContext is not MaaRoiPreviewRow row
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.BeginRoiDrag(row, RoiPointerX(e), RoiPointerY(e));
        _roiDragSource = control;
        _roiActivePointer = e.Pointer;
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void RoiOverlayPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(sender, _roiDragSource) || DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.UpdateRoiDrag(RoiPointerX(e), RoiPointerY(e));
        e.Handled = true;
    }

    private void RoiOverlayPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(sender, _roiDragSource) || DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.EndRoiDrag();
        e.Pointer.Capture(null);
        _roiDragSource = null;
        _roiActivePointer = null;
        e.Handled = true;
    }

    private void RoiOverlayPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!ReferenceEquals(sender, _roiDragSource) || DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.EndRoiDrag();
        _roiDragSource = null;
        _roiActivePointer = null;
    }

    private void RoiResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control control
            || control.DataContext is not MaaRoiPreviewRow row
            || DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        viewModel.BeginRoiResize(row, RoiPointerX(e), RoiPointerY(e), control.Tag as string);
        _roiResizeSource = control;
        _roiActivePointer = e.Pointer;
        e.Pointer.Capture(control);
        e.Handled = true;
    }

    private void RoiResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (!ReferenceEquals(sender, _roiResizeSource) || DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.UpdateRoiResize(RoiPointerX(e), RoiPointerY(e));
        e.Handled = true;
    }

    private void RoiResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!ReferenceEquals(sender, _roiResizeSource) || DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.EndRoiResize();
        e.Pointer.Capture(null);
        _roiResizeSource = null;
        _roiActivePointer = null;
        e.Handled = true;
    }

    private void RoiResizePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!ReferenceEquals(sender, _roiResizeSource) || DataContext is not MainWindowViewModel viewModel)
            return;

        viewModel.EndRoiResize();
        _roiResizeSource = null;
        _roiActivePointer = null;
    }

    private void CancelRoiInteractionOnEscape(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape || DataContext is not MainWindowViewModel viewModel)
            return;

        if (_roiDragSource is null && _roiResizeSource is null)
            return;

        viewModel.CancelRoiInteraction();
        _roiActivePointer?.Capture(null);
        _roiActivePointer = null;
        _roiDragSource = null;
        _roiResizeSource = null;
        e.Handled = true;
    }

    private double RoiPointerX(PointerEventArgs e)
    {
        return e.GetPosition(RoiCanvas).X;
    }

    private double RoiPointerY(PointerEventArgs e)
    {
        return e.GetPosition(RoiCanvas).Y;
    }
}
