using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryThemeTokenControl : Button, IDictionaryValueControl
{
    private readonly FieldDefinition _definition;
    private readonly Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? _showThemeTokenPicker;
    private string _value;

    public DictionaryThemeTokenControl(
        FieldDefinition definition,
        string value,
        Func<string, IReadOnlyList<FieldOption>?, Task<string?>>? showThemeTokenPicker)
    {
        _definition = definition;
        _value = value;
        _showThemeTokenPicker = showThemeTokenPicker;

        Content = ContentForValue(value);
        MinHeight = 36;
        MinWidth = 260;
        HorizontalAlignment = HorizontalAlignment.Left;
        IsEnabled = definition.IsEditable && showThemeTokenPicker is not null;
        Click += async (_, _) => await ShowPicker();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public void SetValue(string value)
    {
        _value = value;
        Content = ContentForValue(value);
    }

    private async Task ShowPicker()
    {
        if (_showThemeTokenPicker is null) return;

        var selected = await _showThemeTokenPicker(_value, _definition.Options);
        if (string.IsNullOrWhiteSpace(selected)) return;

        SetValue(selected);
        ValueChanged?.Invoke(this, _value);
        ValueCommitted?.Invoke(this, _value);
    }

    private static Control ContentForValue(string value)
    {
        return new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "Select theme token..." : value,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}
