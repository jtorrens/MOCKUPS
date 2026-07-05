using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed class EditorInstantComboBox : Grid
{
    private readonly Button _button;
    private readonly Popup _popup;
    private readonly StackPanel _itemsPanel;
    private IReadOnlyList<FieldOption> _items = [];
    private FieldOption? _selectedItem;

    public EditorInstantComboBox()
    {
        _button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = 36,
            Padding = new Thickness(10, 6),
        };
        _button.Click += (_, _) => TogglePopup();
        Children.Add(_button);

        _itemsPanel = new StackPanel
        {
            Spacing = 1,
        };
        _popup = new Popup
        {
            PlacementTarget = _button,
            Placement = PlacementMode.Bottom,
            IsLightDismissEnabled = true,
            Child = new Border
            {
                MinWidth = 112,
                Padding = new Thickness(4),
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.Parse("#F01A2433")),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.Parse("#55708AA8")),
                Child = _itemsPanel,
            },
        };
        Children.Add(_popup);
        UpdateButtonContent();
    }

    public event EventHandler? SelectionChanged;

    internal IEnumerable<FieldOption>? ItemsSource
    {
        get => _items;
        set
        {
            _items = value?.ToList() ?? [];
            RebuildItems();
        }
    }

    internal FieldOption? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (Equals(_selectedItem, value)) return;

            _selectedItem = value;
            UpdateButtonContent();
            RebuildItems();
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void TogglePopup()
    {
        if (_items.Count == 0) return;

        if (_popup.Child is Border border)
        {
            border.Width = Math.Max(_button.Bounds.Width, 112);
        }

        _popup.IsOpen = !_popup.IsOpen;
    }

    private void Select(FieldOption option)
    {
        _popup.IsOpen = false;
        SelectedItem = option;
    }

    private void RebuildItems()
    {
        _itemsPanel.Children.Clear();
        foreach (var item in _items)
        {
            var isSelected = Equals(item, _selectedItem);
            var button = new Button
            {
                Content = item.Label,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                MinHeight = 30,
                Padding = new Thickness(8, 5),
                Background = isSelected ? new SolidColorBrush(Color.Parse("#223388FF")) : Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            button.Click += (_, _) => Select(item);
            _itemsPanel.Children.Add(button);
        }
    }

    private void UpdateButtonContent()
    {
        _button.Content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Children =
            {
                new TextBlock
                {
                    Text = _selectedItem?.Label ?? "",
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                },
                new TextBlock
                {
                    Text = "v",
                    Width = 18,
                    TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Opacity = 0.72,
                    [Grid.ColumnProperty] = 1,
                },
            },
        };
    }
}
