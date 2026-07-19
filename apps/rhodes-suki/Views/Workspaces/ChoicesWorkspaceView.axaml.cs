using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using RhodesSuki.Services;

namespace RhodesSuki.Views.Workspaces;

public partial class ChoicesWorkspaceView : UserControl
{
    private Control? _dragSource;
    private ScrollViewer? _dragScrollViewer;
    private Point _dragStartPoint;
    private Vector _dragStartOffset;
    private bool _isDragging;

    public ChoicesWorkspaceView()
    {
        InitializeComponent();
        RegisterChoicePaneDrag(OperatorChoicePane);
        RegisterChoicePaneDrag(RelicChoicePane);
    }

    private void RegisterChoicePaneDrag(ItemsControl pane)
    {
        pane.AddHandler(PointerPressedEvent, ChoicePanePointerPressed, RoutingStrategies.Tunnel, true);
        pane.AddHandler(PointerMovedEvent, ChoicePanePointerMoved, RoutingStrategies.Tunnel, true);
        pane.AddHandler(PointerReleasedEvent, ChoicePanePointerReleased, RoutingStrategies.Tunnel, true);
        pane.AddHandler(PointerCaptureLostEvent, ChoicePanePointerCaptureLost, RoutingStrategies.Tunnel, true);
    }

    private void ChoicePanePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control source
            || !e.GetCurrentPoint(source).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _dragSource = source;
        _dragScrollViewer = source.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        _dragStartPoint = e.GetPosition(source);
        _dragStartOffset = _dragScrollViewer?.Offset ?? default;
        _isDragging = false;
    }

    private void ChoicePanePointerMoved(object? sender, PointerEventArgs e)
    {
        if (sender is not Control source
            || !ReferenceEquals(source, _dragSource)
            || _dragScrollViewer is null
            || !e.GetCurrentPoint(source).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var delta = e.GetPosition(source) - _dragStartPoint;
        if (!_isDragging)
        {
            if (Math.Abs(delta.Y) < RhodesChoicePaneDragScroll.StartThreshold)
                return;

            _isDragging = true;
            e.Pointer.Capture(source);
        }

        var nextOffset = RhodesChoicePaneDragScroll.OffsetForDrag(
            _dragStartOffset.Y,
            delta.Y,
            _dragScrollViewer.Extent.Height,
            _dragScrollViewer.Viewport.Height);
        _dragScrollViewer.Offset = new Vector(_dragStartOffset.X, nextOffset);
        e.Handled = true;
    }

    private void ChoicePanePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (sender is not Control source || !ReferenceEquals(source, _dragSource))
            return;

        if (_isDragging)
            e.Handled = true;
        if (ReferenceEquals(e.Pointer.Captured, source))
            e.Pointer.Capture(null);
        ResetChoicePaneDrag();
    }

    private void ChoicePanePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) =>
        ResetChoicePaneDrag();

    private void ResetChoicePaneDrag()
    {
        _dragSource = null;
        _dragScrollViewer = null;
        _dragStartPoint = default;
        _dragStartOffset = default;
        _isDragging = false;
    }
}
