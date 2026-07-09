using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using RhodesSuki.ViewModels;

namespace RhodesSuki.Views;

public partial class HudWindow : Window
{
    private const int GwlExStyle = -20;
    private const nint WsExTransparent = 0x00000020;
    private const nint WsExLayered = 0x00080000;

    private MainWindowViewModel? _viewModel;

    public HudWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        PositionChanged += OnPositionChanged;
        Opened += OnOpened;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (_viewModel is { HudX: >= 0, HudY: >= 0 })
            Position = new PixelPoint(_viewModel.HudX, _viewModel.HudY);

        ApplyClickThrough(_viewModel?.IsHudClickThrough ?? false);
    }

    private void OnPositionChanged(object? sender, PixelPointEventArgs e)
    {
        _viewModel?.UpdateHudPosition(e.Point.X, e.Point.Y);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsHudClickThrough))
            ApplyClickThrough(_viewModel?.IsHudClickThrough ?? false);
    }

    private void GripPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void CloseClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.IsHudVisible = false;
        else
            Hide();
    }

    /// <summary>
    /// Win32の拡張スタイルでマウス入力を素通しさせる。
    /// WS_EX_TRANSPARENT はヒットテストを完全に無効化するため、解除は外部 (メインウィンドウ) から行う。
    /// </summary>
    private void ApplyClickThrough(bool enabled)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var handle = TryGetPlatformHandle()?.Handle ?? nint.Zero;
        if (handle == nint.Zero)
            return;

        var style = GetWindowLongPtrW(handle, GwlExStyle);
        style = enabled
            ? style | WsExTransparent | WsExLayered
            : style & ~WsExTransparent;
        SetWindowLongPtrW(handle, GwlExStyle, style);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtrW(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern nint SetWindowLongPtrW(nint hWnd, int nIndex, nint dwNewLong);
}
