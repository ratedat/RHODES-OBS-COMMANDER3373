using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace RhodesSuki.Controls;

public sealed class RhodesNumericUpDown : NumericUpDown
{
    protected override Type StyleKeyOverride => typeof(NumericUpDown);

    protected override AutomationPeer OnCreateAutomationPeer() => new NumericInputPeer(this);

    // Avalonia 12.0.5 sends decimal change values to Windows UIA, which rejects them.
    // Keep the range-value interface and send the doubles required by that interface.
    // https://github.com/AvaloniaUI/Avalonia/blob/12.0.5/src/Avalonia.Controls/Automation/Peers/NumericUpDownAutomationPeer.cs
    private sealed class NumericInputPeer : ControlAutomationPeer, IRangeValueProvider
    {
        private readonly RhodesNumericUpDown _input;

        public NumericInputPeer(RhodesNumericUpDown input) : base(input)
        {
            _input = input;
            input.PropertyChanged += InputPropertyChanged;
        }

        public bool IsReadOnly => _input.IsReadOnly;
        public double Minimum => (double)_input.Minimum;
        public double Maximum => (double)_input.Maximum;
        public double SmallChange => (double)_input.Increment;
        public double LargeChange => SmallChange;
        public double Value => ToRangeValue(_input.Value);

        public void SetValue(double value)
        {
            EnsureEnabled();
            if (IsReadOnly)
                throw new InvalidOperationException("The numeric input is read-only.");
            if (!double.IsFinite(value) || value < Minimum || value > Maximum)
                throw new ArgumentOutOfRangeException(nameof(value));
            _input.SetCurrentValue(ValueProperty, (decimal)value);
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Spinner;
        protected override string GetClassNameCore() => nameof(NumericUpDown);

        private double ToRangeValue(object? value) =>
            (double)(value is decimal number ? number : Math.Clamp(0m, _input.Minimum, _input.Maximum));

        private void InputPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
        {
            if (change.Property == IsReadOnlyProperty)
            {
                RaisePropertyChangedEvent(RangeValuePatternIdentifiers.IsReadOnlyProperty, change.OldValue, change.NewValue);
                return;
            }

            var property = change.Property == ValueProperty ? RangeValuePatternIdentifiers.ValueProperty
                : change.Property == MinimumProperty ? RangeValuePatternIdentifiers.MinimumProperty
                : change.Property == MaximumProperty ? RangeValuePatternIdentifiers.MaximumProperty
                : null;
            if (property is null)
                return;

            var previous = ToRangeValue(change.OldValue);
            var next = ToRangeValue(change.NewValue);
            RaisePropertyChangedEvent(property, previous, next);
        }
    }
}
