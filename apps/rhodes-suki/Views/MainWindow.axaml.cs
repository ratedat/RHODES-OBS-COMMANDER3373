using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using SukiUI.Controls;

namespace RhodesSuki.Views;

public partial class MainWindow : SukiWindow
{
    public MainWindow()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, CloseOpenComboBoxesOnOutsidePress, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void CloseOpenComboBoxesOnOutsidePress(object? sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Avalonia.Visual source)
            return;

        var clickedComboBox = source as ComboBox ?? source.GetVisualAncestors().OfType<ComboBox>().FirstOrDefault();
        foreach (var comboBox in this.GetVisualDescendants().OfType<ComboBox>())
        {
            if (ReferenceEquals(comboBox, clickedComboBox))
                continue;

            comboBox.IsDropDownOpen = false;
        }
    }
}
