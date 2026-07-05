using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed class EditorInstantComboBox : Grid
{
    private readonly Button _button;
    private readonly TextBlock _label;
    private readonly TextBlock _indicator;
    private readonly Popup _popup;
    private readonly StackPanel _itemsPanel;
    private TopLevel? _dismissRoot;
    private IReadOnlyList<FieldOption> _items = [];
    private FieldOption? _selectedItem;
    private int _highlightedIndex = -1;

    public EditorInstantComboBox()
    {
        Focusable = true;
        ColumnDefinitions = new ColumnDefinitions("*,22");
        KeyDown += OnKeyDown;

        _button = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            MinHeight = 36,
            Padding = new Thickness(10, 6),
            Focusable = false,
        };
        _button.Click += (_, _) => TogglePopup();
        Grid.SetColumnSpan(_button, 2);
        Children.Add(_button);

        _label = new TextBlock
        {
            Margin = new Thickness(10, 0, 4, 0),
            IsHitTestVisible = false,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        _indicator = new TextBlock
        {
            Text = ">",
            Width = 16,
            Margin = new Thickness(0, 0, 10, 0),
            FontSize = 13,
            FontWeight = FontWeight.Bold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            Opacity = 0.72,
            IsHitTestVisible = false,
        };
        Grid.SetColumn(_label, 0);
        Grid.SetColumn(_indicator, 1);
        Children.Add(_label);
        Children.Add(_indicator);

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

        SetPopupOpen(!_popup.IsOpen);
    }

    private void SetPopupOpen(bool isOpen)
    {
        if (isOpen && _popup.Child is Border border)
        {
            border.Width = Math.Max(_button.Bounds.Width, 112);
            _highlightedIndex = SelectedIndex();
        }

        _popup.IsOpen = isOpen;
        _indicator.Text = isOpen ? "v" : ">";
        RebuildItems();
        if (isOpen)
        {
            AttachDismissHandler();
        }
        else
        {
            DetachDismissHandler();
        }
    }

    private void Select(FieldOption option)
    {
        SetPopupOpen(false);
        SelectedItem = option;
    }

    private void SelectHighlighted()
    {
        if (_highlightedIndex < 0 || _highlightedIndex >= _items.Count) return;

        Select(_items[_highlightedIndex]);
    }

    private void MoveHighlight(int delta)
    {
        if (_items.Count == 0) return;

        if (!_popup.IsOpen)
        {
            SetPopupOpen(true);
        }

        var index = _highlightedIndex < 0 ? SelectedIndex() : _highlightedIndex;
        if (index < 0) index = 0;
        index = Math.Clamp(index + delta, 0, _items.Count - 1);
        if (_highlightedIndex == index) return;

        _highlightedIndex = index;
        RebuildItems();
    }

    private int SelectedIndex()
    {
        if (_selectedItem is null) return _items.Count > 0 ? 0 : -1;
        var index = _items.ToList().FindIndex((item) => Equals(item, _selectedItem));
        return index >= 0 ? index : (_items.Count > 0 ? 0 : -1);
    }

    private void OnKeyDown(object? sender, KeyEventArgs args)
    {
        switch (args.Key)
        {
            case Key.Enter:
            case Key.Space:
                if (_popup.IsOpen)
                {
                    SelectHighlighted();
                }
                else
                {
                    SetPopupOpen(true);
                }
                args.Handled = true;
                break;
            case Key.Down:
                MoveHighlight(1);
                args.Handled = true;
                break;
            case Key.Up:
                MoveHighlight(-1);
                args.Handled = true;
                break;
            case Key.Escape:
                SetPopupOpen(false);
                args.Handled = true;
                break;
        }
    }

    private void AttachDismissHandler()
    {
        var root = TopLevel.GetTopLevel(this);
        if (root is null || ReferenceEquals(root, _dismissRoot)) return;

        DetachDismissHandler();
        _dismissRoot = root;
        root.AddHandler(PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private void DetachDismissHandler()
    {
        if (_dismissRoot is null) return;

        _dismissRoot.RemoveHandler(PointerPressedEvent, OnRootPointerPressed);
        _dismissRoot = null;
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs args)
    {
        if (args.Source is Visual source
            && (_button.IsVisualAncestorOf(source)
                || (_popup.Child is Visual popupChild && popupChild.IsVisualAncestorOf(source))))
        {
            return;
        }

        SetPopupOpen(false);
    }

    private void RebuildItems()
    {
        _itemsPanel.Children.Clear();
        for (var index = 0; index < _items.Count; index++)
        {
            var item = _items[index];
            var isSelected = Equals(item, _selectedItem);
            var isHighlighted = _popup.IsOpen && index == _highlightedIndex;
            var button = new Button
            {
                Content = item.Label,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                MinHeight = 30,
                Padding = new Thickness(8, 5),
                Background = isHighlighted
                    ? new SolidColorBrush(Color.Parse("#334B8DFF"))
                    : isSelected ? new SolidColorBrush(Color.Parse("#223388FF")) : Brushes.Transparent,
                Cursor = new Cursor(StandardCursorType.Hand),
                Focusable = false,
            };
            button.Click += (_, _) => Select(item);
            _itemsPanel.Children.Add(button);
        }
    }

    private void UpdateButtonContent()
    {
        _label.Text = _selectedItem?.Label ?? "";
    }
}
