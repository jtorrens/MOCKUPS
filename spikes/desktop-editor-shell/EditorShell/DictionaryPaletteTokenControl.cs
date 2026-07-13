using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class DictionaryPaletteTokenControl : Button, IDictionaryValueControl
{
    private readonly IReadOnlyList<FieldOption> _options;
    private readonly string _title;
    private string _value;

    public DictionaryPaletteTokenControl(
        string title,
        IReadOnlyList<FieldOption>? options,
        string value,
        bool isEditable)
    {
        _title = title;
        _options = options ?? [];
        _value = value;

        MinHeight = 36;
        MinWidth = 180;
        HorizontalAlignment = HorizontalAlignment.Left;
        HorizontalContentAlignment = HorizontalAlignment.Stretch;
        Padding = new Avalonia.Thickness(10, 6);
        BorderThickness = new Avalonia.Thickness(0);
        CornerRadius = new Avalonia.CornerRadius(8);
        IsEnabled = isEditable;
        Content = ContentForValue(value);
        ActualThemeVariantChanged += (_, _) => ApplyThemeBrushes();
        ApplyThemeBrushes();
        Click += async (_, _) =>
        {
            var owner = TopLevel.GetTopLevel(this) as Window;
            if (owner is null) return;

            var selected = await PaletteColorPickerDialog.Show(owner, _title, _options, _value);
            if (selected is null) return;

            SetValue(selected);
            ValueChanged?.Invoke(this, _value);
            ValueCommitted?.Invoke(this, _value);
        };
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public string Value => _value;

    public void SetValue(string value)
    {
        _value = value;
        Content = ContentForValue(value);
    }

    public void UseCompactWidth()
    {
        MinWidth = 0;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        Content = ContentForValue(_value);
    }

    private Control ContentForValue(string value)
    {
        var option = _options.FirstOrDefault((candidate) => candidate.Value == value);
        var swatch = PaletteColorPickerDialog.Swatch(option?.ColorHex, 18, option?.IsNeutral == true);
        var label = new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(option?.Label) ? value : option.Label,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
            MaxWidth = 142,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        Grid.SetColumn(label, 1);
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
        Grid.SetColumn(chevron, 2);

        return new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("24,*,Auto"),
            ColumnSpacing = 8,
            Children =
            {
                swatch,
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
