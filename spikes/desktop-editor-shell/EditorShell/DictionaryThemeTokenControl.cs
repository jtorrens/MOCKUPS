using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
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
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Padding = new Avalonia.Thickness(10, 6);
        BorderThickness = new Avalonia.Thickness(0);
        IsEnabled = definition.IsEditable && showThemeTokenPicker is not null;
        Click += async (_, _) => await ShowPicker();
        ActualThemeVariantChanged += (_, _) => ApplyThemeBrushes();
        ApplyThemeBrushes();
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
        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(value) ? "Select theme token..." : value,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(label, 0);

        var chevron = new TextBlock
        {
            Text = ">",
            Width = 16,
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72,
        };
        Grid.SetColumn(chevron, 1);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                label,
                chevron,
            },
        };
    }

    private void ApplyThemeBrushes()
    {
        var isLight = ActualThemeVariant == ThemeVariant.Light;
        Background = new SolidColorBrush(Color.Parse(isLight ? "#14000000" : "#10FFFFFF"));
    }
}
