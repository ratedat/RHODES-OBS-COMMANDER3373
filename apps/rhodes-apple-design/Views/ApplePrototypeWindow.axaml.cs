using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace RhodesAppleDesign.Views;

public partial class ApplePrototypeWindow : Window
{
    private const double OverlayBaseWidth = 960;
    private const double OverlayBaseHeight = 540;
    private static readonly HashSet<string> RecordedOperatorIds = new(StringComparer.Ordinal)
    {
        "fang",
        "kroos",
        "wang",
        "yato",
        "adnachiel",
        "pramanix2",
        "orchid",
        "hibiscus",
        "spot",
        "ch_en3",
        "yato2",
    };
    private static readonly int[] RecordedRelicNumbers = [1, 222, 228, 262, 189, 231, 83, 194, 218, 223, 227, 75];
    private static readonly Dictionary<int, string> RecordedRelicResources = new()
    {
        [1] = "real_relic_001.png",
        [75] = "real_relic_075.png",
        [83] = "real_relic_083.png",
        [189] = "real_relic_189.png",
        [194] = "real_relic_194.png",
        [218] = "real_relic_218.png",
        [222] = "real_relic_222.png",
        [223] = "real_relic_223.png",
        [227] = "real_relic_227.png",
        [228] = "real_relic_228.png",
        [231] = "real_relic_231.png",
        [262] = "real_relic_262.png",
    };
    private static readonly Dictionary<string, int> OperatorClassOrder = new(StringComparer.Ordinal)
    {
        ["先鋒"] = 0,
        ["前衛"] = 1,
        ["重装"] = 2,
        ["狙撃"] = 3,
        ["術師"] = 4,
        ["医療"] = 5,
        ["補助"] = 6,
        ["特殊"] = 7,
    };
    private static readonly Dictionary<string, string[]> OperatorBranchOrderByClass = new(StringComparer.Ordinal)
    {
        ["先鋒"] = ["先駆兵", "突撃兵", "戦術家", "旗手", "偵察兵", "策士"],
        ["前衛"] = ["強襲者", "闘士", "術戦士", "教官", "領主", "剣豪", "武者", "勇士", "鎌撃士", "解放者", "重剣士", "槌撃士", "本源戦士", "傭兵"],
        ["重装"] = ["重盾衛士", "庇護衛士", "破壊者", "術技衛士", "決闘者", "堅城砲手", "哨戒衛士", "本源衛士"],
        ["狙撃"] = ["速射手", "精密射手", "榴弾射手", "戦術射手", "散弾射手", "破城射手", "投擲手", "狩人", "旋輪射手", "翔空射手"],
        ["術師"] = ["中堅術師", "拡散術師", "操機術師", "法陣術師", "秘術師", "連鎖術師", "爆撃術師", "本源術師", "創霊術師"],
        ["医療"] = ["医師", "群癒師", "療養師", "放浪医", "呪癒師", "連鎖癒師", "守望者"],
        ["補助"] = ["緩速師", "呪詛師", "吟遊者", "祈祷師", "召喚師", "工匠", "祭儀師"],
        ["特殊"] = ["執行者", "推撃手", "潜伏者", "鉤縄師", "鬼才", "行商人", "罠師", "傀儡師", "錬金士", "巡空者"],
    };

    private readonly List<OperatorCatalogItem> _allOperators = [];
    private readonly List<RelicCatalogItem> _allRelics = [];
    private Control? _dragTarget;
    private Control? _resizeTarget;
    private Control? _resizeHandle;
    private Control? _selectedBlock;
    private Point _dragPointerStart;
    private Point _dragOrigin;
    private Point _resizePointerStart;
    private Size _resizeOrigin;
    private bool _isRefreshingOperatorBranchFilter;

    public ObservableCollection<OperatorCatalogRow> OperatorRows { get; } = [];
    public ObservableCollection<RelicCatalogRow> RelicRows { get; } = [];

    public ApplePrototypeWindow()
    {
        InitializeComponent();
        DataContext = this;
        LoadOperatorCatalog();
        LoadRelicCatalog();
        _selectedBlock = SpecialBlock;
        ShowWorkspace("Run");
    }

    private void LoadOperatorCatalog()
    {
        using var stream = AssetLoader.Open(new Uri("avares://RhodesAppleDesignPrototype/Data/operators.json"));
        using var document = JsonDocument.Parse(stream);

        foreach (var element in document.RootElement.GetProperty("operators").EnumerateArray())
        {
            if (element.GetProperty("isJapanUnreleased").GetBoolean())
            {
                continue;
            }

            var id = element.GetProperty("id").GetString() ?? string.Empty;
            var imagePath = element.GetProperty("image").GetProperty("sourcePath").GetString() ?? string.Empty;
            _allOperators.Add(new OperatorCatalogItem(
                id,
                element.GetProperty("name").GetString() ?? id,
                element.GetProperty("rarity").GetInt32(),
                element.GetProperty("class").GetString() ?? "不明",
                element.GetProperty("branch").GetString() ?? "不明",
                imagePath,
                element.GetProperty("displayOrder").GetInt32(),
                RecordedOperatorIds.Contains(id)));
        }

        RefreshOperatorBranchFilter();
        ApplyOperatorFilters();
    }

    private void RefreshOperatorBranchFilter()
    {
        if (OperatorBranchFilter is null || OperatorClassFilter is null)
        {
            return;
        }

        var selectedClass = (OperatorClassFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var branches = _allOperators
            .Where(item => selectedClass is null or "すべての職業" || item.ClassName == selectedClass)
            .Select(item => (item.Branch, item.ClassName))
            .GroupBy(item => item.Branch, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => OperatorClassOrder.GetValueOrDefault(item.ClassName, int.MaxValue))
            .ThenBy(item => OperatorBranchRank(item.ClassName, item.Branch))
            .ThenBy(item => item.Branch, StringComparer.CurrentCulture)
            .Select(item => item.Branch)
            .ToArray();

        _isRefreshingOperatorBranchFilter = true;
        OperatorBranchFilter.ItemsSource = new[] { "すべての職分" }.Concat(branches).ToArray();
        OperatorBranchFilter.SelectedIndex = 0;
        _isRefreshingOperatorBranchFilter = false;
    }

    private static int OperatorBranchRank(string className, string branch)
    {
        if (!OperatorBranchOrderByClass.TryGetValue(className, out var branches))
        {
            return int.MaxValue;
        }

        var rank = Array.IndexOf(branches, branch);
        return rank >= 0 ? rank : int.MaxValue;
    }

    private void LoadRelicCatalog()
    {
        using var stream = AssetLoader.Open(new Uri("avares://RhodesAppleDesignPrototype/Data/relics.json"));
        using var document = JsonDocument.Parse(stream);

        foreach (var element in document.RootElement.GetProperty("relics").EnumerateArray())
        {
            if (element.GetProperty("campaignId").GetString() != "is3_mizuki")
            {
                continue;
            }

            var number = element.GetProperty("number").GetInt32();
            if (!RecordedRelicResources.TryGetValue(number, out var resourceName))
            {
                continue;
            }

            _allRelics.Add(new RelicCatalogItem(
                element.GetProperty("id").GetString() ?? $"is3_mizuki_relic_{number:000}",
                element.GetProperty("name").GetString() ?? $"No.{number:000}",
                number,
                element.GetProperty("category").GetString() ?? "種別不明",
                resourceName,
                Array.IndexOf(RecordedRelicNumbers, number),
                isSelected: true,
                isUsed: number == 228));
        }

        RelicCategoryFilter.ItemsSource = new[] { "すべての秘宝種別" }
            .Concat(_allRelics.Select(item => item.Category).Distinct(StringComparer.Ordinal).OrderBy(category => category, StringComparer.CurrentCulture))
            .ToArray();
        RelicCategoryFilter.SelectedIndex = 0;
        ApplyRelicFilters();
    }

    private void ApplyOperatorFilters()
    {
        if (OperatorSearchBox is null
            || OperatorClassFilter is null
            || OperatorBranchFilter is null
            || OperatorRarityFilter is null
            || OperatorSortFilter is null)
        {
            return;
        }

        var query = OperatorSearchBox.Text?.Trim() ?? string.Empty;
        var classFilter = (OperatorClassFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var branchFilter = OperatorBranchFilter.SelectedItem?.ToString();
        var rarityLabel = (OperatorRarityFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var sortLabel = (OperatorSortFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var rarityFilter = rarityLabel is { Length: 2 } && int.TryParse(rarityLabel[1..], out var rarity)
            ? rarity
            : (int?)null;

        var filtered = _allOperators
            .Where(item => string.IsNullOrEmpty(query)
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.ClassName.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Branch.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(item => classFilter is null or "すべての職業" || item.ClassName == classFilter)
            .Where(item => branchFilter is null or "すべての職分" || item.Branch == branchFilter)
            .Where(item => rarityFilter is null || item.Rarity == rarityFilter)
            .ToArray();

        var selectedFirst = filtered.OrderByDescending(item => item.IsSelected);
        var ordered = sortLabel switch
        {
            "職業・職分順" => selectedFirst
                .ThenBy(item => OperatorClassOrder.GetValueOrDefault(item.ClassName, int.MaxValue))
                .ThenBy(item => OperatorBranchRank(item.ClassName, item.Branch))
                .ThenBy(item => item.Branch, StringComparer.CurrentCulture)
                .ThenByDescending(item => item.Rarity)
                .ThenBy(item => item.DisplayOrder),
            "名前順" => selectedFirst
                .ThenBy(item => item.Name, StringComparer.CurrentCulture)
                .ThenByDescending(item => item.Rarity),
            _ => selectedFirst
                .ThenByDescending(item => item.Rarity)
                .ThenBy(item => item.DisplayOrder),
        };
        filtered = ordered.ToArray();

        OperatorRows.Clear();
        foreach (var row in filtered.Chunk(4))
        {
            OperatorRows.Add(new OperatorCatalogRow(row));
        }

        UpdateOperatorCatalogCount(filtered.Length);
    }

    private void ApplyRelicFilters()
    {
        if (OperatorSearchBox is null || RelicCategoryFilter is null || RelicSortFilter is null)
        {
            return;
        }

        var query = OperatorSearchBox.Text?.Trim() ?? string.Empty;
        var categoryFilter = RelicCategoryFilter.SelectedItem?.ToString();
        var sortLabel = (RelicSortFilter.SelectedItem as ComboBoxItem)?.Content?.ToString();
        var filtered = _allRelics
            .Where(item => string.IsNullOrEmpty(query)
                || item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Category.Contains(query, StringComparison.OrdinalIgnoreCase)
                || item.Number.ToString("000").Contains(query, StringComparison.OrdinalIgnoreCase))
            .Where(item => categoryFilter is null or "すべての秘宝種別" || item.Category == categoryFilter)
            .ToArray();

        var selectedFirst = filtered.OrderByDescending(item => item.IsSelected);
        var ordered = sortLabel switch
        {
            "番号順" => selectedFirst.ThenBy(item => item.Number),
            "入手順" => selectedFirst.ThenBy(item => item.RecordedOrder),
            _ => selectedFirst.ThenBy(item => item.Category, StringComparer.CurrentCulture).ThenBy(item => item.Number),
        };
        filtered = ordered.ToArray();

        RelicRows.Clear();
        foreach (var row in filtered.Chunk(4))
        {
            RelicRows.Add(new RelicCatalogRow(row));
        }

        var selectedCount = _allRelics.Count(item => item.IsSelected);
        CatalogCountText.Text = $"IS#3実取得 {_allRelics.Count}件 · 表示 {filtered.Length}件 · 選択 {selectedCount}件";
    }

    private void UpdateOperatorCatalogCount(int visibleCount)
    {
        var selectedCount = _allOperators.Count(item => item.IsSelected);
        CatalogCountText.Text = $"日本実装 {_allOperators.Count}名 · 表示 {visibleCount}名 · 選択 {selectedCount}名";
    }

    private void OnCatalogFilterChanged(object? sender, RoutedEventArgs e)
    {
        if (_isRefreshingOperatorBranchFilter)
        {
            return;
        }

        if (ReferenceEquals(sender, OperatorClassFilter))
        {
            RefreshOperatorBranchFilter();
        }

        if (RelicCatalogPanel?.IsVisible == true)
        {
            ApplyRelicFilters();
        }
        else
        {
            ApplyOperatorFilters();
        }
    }

    private void OnRelicCatalogItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: RelicCatalogItem item })
        {
            return;
        }

        item.IsSelected = !item.IsSelected;
        ApplyRelicFilters();
        GlobalStatusText.Text = $"{item.Name}を{(item.IsSelected ? "選択" : "解除")}しました（デザインモック）。";
    }

    private void OnOperatorCatalogItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { CommandParameter: OperatorCatalogItem item })
        {
            return;
        }

        item.IsSelected = !item.IsSelected;
        ApplyOperatorFilters();
        GlobalStatusText.Text = $"{item.Name}を{(item.IsSelected ? "選択" : "解除")}しました（デザインモック）。";
    }

    private void OnNavigate(object? sender, RoutedEventArgs e)
    {
        if (sender is RadioButton { IsChecked: true } button && button.Tag is string workspace)
        {
            ShowWorkspace(workspace);
        }
    }

    private void OnNavigateButton(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string workspace })
        {
            return;
        }

        var navigation = workspace switch
        {
            "Special" => SpecialNavigation,
            "Library" => LibraryNavigation,
            "Output" => OutputNavigation,
            _ => RunNavigation,
        };

        navigation.IsChecked = true;
        ShowWorkspace(workspace);
    }

    private void OnCatalogTypeChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not RadioButton { IsChecked: true, Tag: string catalog }
            || OperatorCatalogPanel is null
            || RelicCatalogPanel is null)
        {
            return;
        }

        var showOperators = catalog == "Operators";
        OperatorCatalogPanel.IsVisible = showOperators;
        RelicCatalogPanel.IsVisible = !showOperators;
        OperatorFilterPanel.IsVisible = showOperators;
        RelicFilterPanel.IsVisible = !showOperators;
        OperatorSearchBox.PlaceholderText = showOperators ? "名前・職業・職分で検索" : "秘宝名・番号・秘宝種別で検索";
        if (showOperators)
        {
            ApplyOperatorFilters();
        }
        else
        {
            ApplyRelicFilters();
        }
    }

    private void ShowWorkspace(string workspace)
    {
        RunWorkspace.IsVisible = workspace == "Run";
        SpecialWorkspace.IsVisible = workspace == "Special";
        LibraryWorkspace.IsVisible = workspace == "Library";
        OutputWorkspace.IsVisible = workspace == "Output";

        var outputMode = workspace == "Output";
        NavigationPane.IsVisible = !outputMode;
        ShellGrid.ColumnDefinitions[0].Width = outputMode ? new GridLength(0) : new GridLength(176);

        GlobalStatusText.Text = workspace switch
        {
            "Special" => "テーマ固有値を表示しています。",
            "Library" => "選択カタログを表示しています。",
            "Output" => "出力レイアウト編集モードです。ブロックを直接ドラッグできます。",
            _ => "ランを表示しています。",
        };
    }

    private void OnAcquireClick(object? sender, RoutedEventArgs e)
    {
        ResultBanner.IsVisible = true;
        ResultSummaryText.Text = "実記録を反映 · 源石錐28 · 秘宝12件 · オペレーター11名";
        GlobalStatusText.Text = "画面を取得し、認識結果を反映しました（デザインモック）。";
    }

    private void OnIndividualAcquireClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string acquisition })
        {
            ShowIndividualAcquireResult(acquisition);
        }
    }

    private void OnCatalogAcquireClick(object? sender, RoutedEventArgs e)
    {
        ShowIndividualAcquireResult(RelicCatalogPanel.IsVisible ? "Relics" : "Operators");
    }

    private void ShowIndividualAcquireResult(string acquisition)
    {
        var summary = acquisition switch
        {
            "Basic" => "基本情報を反映 · 源石錐28 · 人文主義分隊",
            "Special" => "特殊値を反映 · 鍵5 · 灯火39 · 呼び声：給養",
            "Operators" => "オペレーターを反映 · 招集済み11名",
            "Relics" => "秘宝を反映 · 所持12件",
            _ => "取得結果を反映",
        };

        ResultBanner.IsVisible = true;
        ResultSummaryText.Text = summary;
        GlobalStatusText.Text = $"{summary}（デザインモック）。";
    }

    private void OnUndoClick(object? sender, RoutedEventArgs e)
    {
        ResultSummaryText.Text = "直前の更新を取り消しました";
        GlobalStatusText.Text = "直前の反映を元に戻しました（デザインモック）。";
    }

    private void OnDismissResult(object? sender, RoutedEventArgs e)
    {
        ResultBanner.IsVisible = false;
        GlobalStatusText.Text = "更新結果を閉じました。";
    }

    private void OnOpenAdbSettings(object? sender, RoutedEventArgs e)
    {
        AdbSettingsOverlay.IsVisible = true;
        GlobalStatusText.Text = "ADB接続設定を開いています。";
    }

    private void OnCloseAdbSettings(object? sender, RoutedEventArgs e)
    {
        AdbSettingsOverlay.IsVisible = false;
        GlobalStatusText.Text = "ADB接続設定を閉じました。";
    }

    private void OnDetectAdbClick(object? sender, RoutedEventArgs e)
    {
        AdbDetectionResult.IsVisible = true;
        GlobalStatusText.Text = "ADB候補を1台検出しました（デザインモック）。";
    }

    private void OnSaveAdbSettings(object? sender, RoutedEventArgs e)
    {
        ConnectionStatusLabel.Text = "接続OK";
        ConnectionStatusDetail.Text = "emulator-5556 · 1280×720";
        AdbSettingsOverlay.IsVisible = false;
        GlobalStatusText.Text = "ADB設定を保存して接続しました（デザインモック）。";
    }

    private void OnActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string message })
        {
            GlobalStatusText.Text = message;
        }
    }

    private void OnResetLayout(object? sender, RoutedEventArgs e)
    {
        SetCanvasPosition(RunBlock, 18, 64);
        SetCanvasPosition(RelicBlock, 276, 64);
        SetCanvasPosition(OperatorBlock, 18, 204);
        SetCanvasPosition(BossBlock, 276, 430);
        SetCanvasPosition(SpecialBlock, 276, 262);
        SetCanvasSize(RunBlock, 238, 118);
        SetCanvasSize(RelicBlock, 404, 176);
        SetCanvasSize(OperatorBlock, 238, 248);
        SetCanvasSize(BossBlock, 664, 106);
        SetCanvasSize(SpecialBlock, 664, 156);
        SelectOverlayBlock(SpecialBlock);
        GlobalStatusText.Text = "レイアウトの位置とサイズを初期値へ戻しました。";
    }

    private static void SetCanvasPosition(Control control, double left, double top)
    {
        Canvas.SetLeft(control, left);
        Canvas.SetTop(control, top);
    }

    private static void SetCanvasSize(Control control, double width, double height)
    {
        control.Width = width;
        control.Height = height;
    }

    private void SelectOverlayBlock(Control target)
    {
        _selectedBlock?.Classes.Remove("selected");
        _selectedBlock = target;
        _selectedBlock.Classes.Add("selected");
        SelectedBlockText.Text = target.Name switch
        {
            nameof(RunBlock) => "ラン情報",
            nameof(RelicBlock) => "秘宝",
            nameof(OperatorBlock) => "オペレーター",
            nameof(BossBlock) => "ボス",
            _ => "鍵・灯火・呼び声",
        };
    }

    private void OnOverlayZoomChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        if (OverlaySurface is null || OverlayZoomLabel is null)
        {
            return;
        }

        var scale = e.NewValue / 100d;
        OverlaySurface.Width = OverlayBaseWidth * scale;
        OverlaySurface.Height = OverlayBaseHeight * scale;
        OverlayZoomLabel.Text = $"{e.NewValue:0}%";
    }

    private void OnOverlayZoomOut(object? sender, RoutedEventArgs e)
    {
        OverlayZoomSlider.Value = Math.Max(OverlayZoomSlider.Minimum, OverlayZoomSlider.Value - 10);
    }

    private void OnOverlayZoomIn(object? sender, RoutedEventArgs e)
    {
        OverlayZoomSlider.Value = Math.Min(OverlayZoomSlider.Maximum, OverlayZoomSlider.Value + 10);
    }

    private void OnFitOverlayClick(object? sender, RoutedEventArgs e)
    {
        var widthScale = Math.Max(0, OverlayViewport.Bounds.Width - 12) / OverlayBaseWidth;
        var heightScale = Math.Max(0, OverlayViewport.Bounds.Height - 12) / OverlayBaseHeight;
        var fitPercent = Math.Clamp(Math.Min(widthScale, heightScale) * 100, OverlayZoomSlider.Minimum, OverlayZoomSlider.Maximum);
        OverlayZoomSlider.Value = Math.Round(fitPercent / 5) * 5;
        GlobalStatusText.Text = $"出力キャンバスを表示領域へフィットしました（{OverlayZoomSlider.Value:0}%）。";
    }

    private Control? ResolveOverlayBlock(string? name) => name switch
    {
        nameof(RunBlock) => RunBlock,
        nameof(RelicBlock) => RelicBlock,
        nameof(OperatorBlock) => OperatorBlock,
        nameof(BossBlock) => BossBlock,
        nameof(SpecialBlock) => SpecialBlock,
        _ => null,
    };

    private void OnResizePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { Tag: string targetName } handle
            || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed
            || ResolveOverlayBlock(targetName) is not { } target)
        {
            return;
        }

        SelectOverlayBlock(target);
        _resizeTarget = target;
        _resizeHandle = handle;
        _resizePointerStart = e.GetPosition(OverlayCanvas);
        _resizeOrigin = new Size(target.Bounds.Width, target.Bounds.Height);
        target.Classes.Add("resizing");
        e.Pointer.Capture(handle);
        e.Handled = true;
        GlobalStatusText.Text = $"{SelectedBlockText.Text}のサイズを変更中です。";
    }

    private void OnResizePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizeTarget is null
            || _resizeHandle is null
            || !ReferenceEquals(e.Pointer.Captured, _resizeHandle))
        {
            return;
        }

        var pointer = e.GetPosition(OverlayCanvas);
        var delta = pointer - _resizePointerStart;
        var left = Canvas.GetLeft(_resizeTarget);
        var top = Canvas.GetTop(_resizeTarget);
        var maxWidth = Math.Max(170, OverlayBaseWidth - left);
        var maxHeight = Math.Max(90, OverlayBaseHeight - top);
        _resizeTarget.Width = Math.Clamp(_resizeOrigin.Width + delta.X, 170, maxWidth);
        _resizeTarget.Height = Math.Clamp(_resizeOrigin.Height + delta.Y, 90, maxHeight);
        e.Handled = true;
    }

    private void OnResizePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizeTarget is null)
        {
            return;
        }

        _resizeTarget.Classes.Remove("resizing");
        e.Pointer.Capture(null);
        _resizeTarget = null;
        _resizeHandle = null;
        e.Handled = true;
        GlobalStatusText.Text = $"{SelectedBlockText.Text}のサイズを更新しました。";
    }

    private void OnOverlayBlockPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control target || !e.GetCurrentPoint(target).Properties.IsLeftButtonPressed)
        {
            return;
        }

        SelectOverlayBlock(target);
        _dragTarget = target;
        _dragPointerStart = e.GetPosition(OverlayCanvas);
        _dragOrigin = new Point(Canvas.GetLeft(target), Canvas.GetTop(target));
        target.Classes.Add("dragging");
        e.Pointer.Capture(target);
        e.Handled = true;
        GlobalStatusText.Text = $"{SelectedBlockText.Text}を移動中です。";
    }

    private void OnOverlayBlockPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragTarget is null || !ReferenceEquals(e.Pointer.Captured, _dragTarget))
        {
            return;
        }

        var pointer = e.GetPosition(OverlayCanvas);
        var delta = pointer - _dragPointerStart;
        var maxLeft = Math.Max(0, OverlayCanvas.Bounds.Width - _dragTarget.Bounds.Width);
        var maxTop = Math.Max(0, OverlayCanvas.Bounds.Height - _dragTarget.Bounds.Height);
        var left = Math.Clamp(_dragOrigin.X + delta.X, 0, maxLeft);
        var top = Math.Clamp(_dragOrigin.Y + delta.Y, 0, maxTop);

        Canvas.SetLeft(_dragTarget, left);
        Canvas.SetTop(_dragTarget, top);
        e.Handled = true;
    }

    private void OnOverlayBlockPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragTarget is null)
        {
            return;
        }

        _dragTarget.Classes.Remove("dragging");
        e.Pointer.Capture(null);
        _dragTarget = null;
        e.Handled = true;
        GlobalStatusText.Text = $"{SelectedBlockText.Text}の位置を更新しました。";
    }
}

public sealed class OperatorCatalogRow(IReadOnlyList<OperatorCatalogItem> items)
{
    public IReadOnlyList<OperatorCatalogItem> Items { get; } = items;
}

public sealed class RelicCatalogRow(IReadOnlyList<RelicCatalogItem> items)
{
    public IReadOnlyList<RelicCatalogItem> Items { get; } = items;
}

public sealed class RelicCatalogItem(
    string id,
    string name,
    int number,
    string category,
    string resourceName,
    int recordedOrder,
    bool isSelected,
    bool isUsed) : INotifyPropertyChanged
{
    private bool _isSelected = isSelected;
    private bool _imageResolved;
    private IImage? _image;

    public string Id { get; } = id;
    public string Name { get; } = name;
    public int Number { get; } = number;
    public string Category { get; } = category;
    public int RecordedOrder { get; } = recordedOrder;
    public string CategoryLabel
    {
        get
        {
            var separator = Category.IndexOf(' ');
            return separator >= 0 && separator + 1 < Category.Length ? Category[(separator + 1)..] : Category;
        }
    }
    public string Detail => $"{(isUsed ? "使用済み · " : string.Empty)}No.{Number:000} · {CategoryLabel}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public IImage? Image
    {
        get
        {
            if (_imageResolved)
            {
                return _image;
            }

            _imageResolved = true;
            try
            {
                using var stream = AssetLoader.Open(new Uri($"avares://RhodesAppleDesignPrototype/Assets/{resourceName}"));
                _image = new Bitmap(stream);
            }
            catch
            {
                _image = null;
            }

            return _image;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class OperatorCatalogItem(
    string id,
    string name,
    int rarity,
    string className,
    string branch,
    string imagePath,
    int displayOrder,
    bool isSelected) : INotifyPropertyChanged
{
    private bool _isSelected = isSelected;
    private bool _imageResolved;
    private IImage? _image;

    public string Id { get; } = id;
    public string Name { get; } = name;
    public int Rarity { get; } = rarity;
    public string ClassName { get; } = className;
    public string Branch { get; } = branch;
    public int DisplayOrder { get; } = displayOrder;
    public string Detail => $"★{Rarity} {ClassName} / {Branch}";

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public IImage? Image
    {
        get
        {
            if (_imageResolved)
            {
                return _image;
            }

            _imageResolved = true;
            var fileName = Path.GetFileName(imagePath);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            try
            {
                var escaped = Uri.EscapeDataString(fileName);
                using var stream = AssetLoader.Open(new Uri($"avares://RhodesAppleDesignPrototype/Assets/Operators/{escaped}"));
                _image = new Bitmap(stream);
            }
            catch
            {
                _image = null;
            }

            return _image;
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
