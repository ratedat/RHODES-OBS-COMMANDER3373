using System.ComponentModel;
using RhodesSuki.ViewModels;
using SukiUI.Controls;

namespace RhodesSuki.Views;

public partial class MainWindow : SukiWindow
{
    private HudWindow? _hudWindow;
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        // 注意: 以前ここにあった「外側クリックで全ComboBoxを閉じる」独自ハンドラは削除した。
        // オーバーレイポップアップ内の項目クリックまで「外側」と判定してしまい、
        // ドロップダウンの項目が選択できなくなる。閉じる動作は標準のlight dismissに任せる。
        DataContextChanged += OnDataContextChanged;
        Opened += (_, _) => SyncHudVisibility();
        Closing += (_, _) =>
        {
            _hudWindow?.Close();
            _hudWindow = null;
        };
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsHudVisible))
            SyncHudVisibility();
    }

    /// <summary>HUD小窓はVMの IsHudVisible を監視して生成・表示・非表示を切り替える。</summary>
    private void SyncHudVisibility()
    {
        if (_viewModel is null)
            return;

        if (_viewModel.IsHudVisible)
        {
            _hudWindow ??= new HudWindow { DataContext = _viewModel };
            _hudWindow.Show();
        }
        else
        {
            _hudWindow?.Hide();
        }
    }
}
