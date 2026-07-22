using Avalonia.Controls;
using Avalonia.Layout;
using Mockups.DesktopEditorShell.Common;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryBooleanControl : Grid, IDictionaryValueControl
{
    private readonly ToggleSwitch _toggleSwitch;
    private bool _isUpdating;

    public DictionaryBooleanControl(string value, bool isEditable)
    {
        _toggleSwitch = new ToggleSwitch
        {
            IsChecked = StringToBool(value),
            IsEnabled = isEditable,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _toggleSwitch.PropertyChanged += (_, change) =>
        {
            if (change.Property != ToggleSwitch.IsCheckedProperty) return;
            if (_isUpdating) return;

            var nextValue = BoolToString(_toggleSwitch.IsChecked == true);
            ValueChanged?.Invoke(this, nextValue);
            ValueCommitted?.Invoke(this, nextValue);
        };
        Children.Add(_toggleSwitch);
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _isUpdating = true;
        _toggleSwitch.IsChecked = StringToBool(value);
        _isUpdating = false;
    }

    private static bool StringToBool(string value)
    {
        return BooleanText.ParseRequired(value, "Boolean dictionary value");
    }

    private static string BoolToString(bool value)
    {
        return BooleanText.Format(value);
    }
}
