using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using RhodesSuki.Controls;

internal static class NumericInputAccessibilityTests
{
    public static void RangeNotifications()
    {
        var input = new RhodesNumericUpDown { Minimum = -10, Maximum = 10, Value = 0.5m };
        var peer = ControlAutomationPeer.CreatePeerForElement(input)!;
        var range = peer.GetProvider<IRangeValueProvider>()!;
        var changes = new List<AutomationPropertyChangedEventArgs>();
        peer.PropertyChanged += (_, change) => changes.Add(change);
        Require(peer.GetAutomationControlType() == AutomationControlType.Spinner && range.Value == 0.5,
            "Numeric input must remain an accessible spinner.");

        input.Value = 1.25m;
        input.Value = null;
        input.Minimum = -5;
        input.Maximum = 20;
        input.Increment = 0.25m;
        var numericChanges = changes.Where(change => change.Property != RangeValuePatternIdentifiers.IsReadOnlyProperty).ToArray();
        Require(numericChanges.Length == 4 && numericChanges.All(change => change.OldValue is double && change.NewValue is double),
            "Windows UIA range notifications must contain doubles, including empty values and changed limits.");
        Require(range.Value == 0 && range.Minimum == -5 && range.Maximum == 20 && range.SmallChange == 0.25,
            "The range provider must expose the current value, limits and increment.");

        range.SetValue(3.5);
        Require(input.Value == 3.5m, "Accessible value changes must reach the input.");
        input.IsReadOnly = true;
        Require(changes.Last().Property == RangeValuePatternIdentifiers.IsReadOnlyProperty
            && changes.Last().NewValue is true, "Read-only changes must remain boolean notifications.");
        try { range.SetValue(4); throw new Exception("Read-only input accepted a value."); }
        catch (InvalidOperationException) { }
        input.IsReadOnly = false;
        try { range.SetValue(21); throw new Exception("Out-of-range input accepted a value."); }
        catch (ArgumentOutOfRangeException) { }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
