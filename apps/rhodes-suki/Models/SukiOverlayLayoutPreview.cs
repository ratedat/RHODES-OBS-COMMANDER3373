using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RhodesSuki.Models;

public sealed class SukiOverlayLayoutPreview : INotifyPropertyChanged
{
    private const int CanvasWidth = 1920;
    private const int CanvasHeight = 1080;
    private const int MinimumWidth = 160;
    private const int MinimumHeight = 80;
    private const double PreviewScale = 0.5;

    private bool _enabled;
    private bool _isSelected;
    private int _x;
    private int _y;
    private int _width;
    private int _height;
    private int _zIndex;

    public SukiOverlayLayoutPreview(string label, SukiOverlayLayoutState state)
    {
        Label = label;
        Id = state.Id;
        _enabled = state.Enabled;
        _width = Math.Clamp(state.Width, MinimumWidth, CanvasWidth);
        _height = Math.Clamp(state.Height, MinimumHeight, CanvasHeight);
        _x = Math.Clamp(state.X, 0, CanvasWidth - _width);
        _y = Math.Clamp(state.Y, 0, CanvasHeight - _height);
        _zIndex = Math.Clamp(state.ZIndex, 1, 6);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }

    public string Label { get; }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (!SetField(ref _isSelected, value))
                return;
            OnPropertyChanged(nameof(SelectionBorderBrush));
        }
    }

    public string SelectionBorderBrush => IsSelected ? "#4FD1B5" : "#3A4B4E";

    public int X
    {
        get => _x;
        set => SetCoordinate(ref _x, Math.Clamp(value, 0, CanvasWidth - Width), nameof(X), nameof(PreviewX));
    }

    public int Y
    {
        get => _y;
        set => SetCoordinate(ref _y, Math.Clamp(value, 0, CanvasHeight - Height), nameof(Y), nameof(PreviewY));
    }

    public int Width
    {
        get => _width;
        set
        {
            var normalized = Math.Clamp(value, MinimumWidth, CanvasWidth);
            if (_width == normalized)
                return;
            _width = normalized;
            if (_x + _width > CanvasWidth)
            {
                _x = CanvasWidth - _width;
                OnPropertyChanged(nameof(X));
                OnPropertyChanged(nameof(PreviewX));
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewWidth));
            OnPropertyChanged(nameof(SizeLabel));
            OnPropertyChanged(nameof(GeometryLabel));
        }
    }

    public int Height
    {
        get => _height;
        set
        {
            var normalized = Math.Clamp(value, MinimumHeight, CanvasHeight);
            if (_height == normalized)
                return;
            _height = normalized;
            if (_y + _height > CanvasHeight)
            {
                _y = CanvasHeight - _height;
                OnPropertyChanged(nameof(Y));
                OnPropertyChanged(nameof(PreviewY));
            }
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewHeight));
            OnPropertyChanged(nameof(SizeLabel));
            OnPropertyChanged(nameof(GeometryLabel));
        }
    }

    public int ZIndex
    {
        get => _zIndex;
        set => SetField(ref _zIndex, Math.Clamp(value, 1, 6));
    }

    public double PreviewX => X * PreviewScale;

    public double PreviewY => Y * PreviewScale;

    public double PreviewWidth => Width * PreviewScale;

    public double PreviewHeight => Height * PreviewScale;

    public string SizeLabel => $"{Width}x{Height}";

    public string GeometryLabel => $"X {X} / Y {Y} / {SizeLabel}";

    public SukiOverlayLayoutState ToState()
    {
        return new SukiOverlayLayoutState(Id, Enabled, X, Y, Width, Height, ZIndex);
    }

    private void SetCoordinate(ref int field, int value, string propertyName, string previewProperty)
    {
        if (field == value)
            return;
        field = value;
        OnPropertyChanged(propertyName);
        OnPropertyChanged(previewProperty);
        OnPropertyChanged(nameof(GeometryLabel));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
