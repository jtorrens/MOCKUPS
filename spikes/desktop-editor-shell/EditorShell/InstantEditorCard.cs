using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class InstantEditorCard : Grid
{
    private readonly TextBlock _indicator;
    private readonly Border _contentHost;
    private bool _isExpanded;

    public InstantEditorCard(Control header, Control content, bool isExpanded, Control? headerTrailing = null)
    {
        SubcardLayout = EditorSubcardLayout.Stacked;
        RowDefinitions = new RowDefinitions("Auto,Auto");

        var headerRow = new Border
        {
            Padding = EditorUiDensity.CardThickness(12, 12),
            MinHeight = EditorUiDensity.Card(62),
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = Brushes.Transparent,
        };
        headerRow.PointerPressed += (_, args) =>
        {
            if (args.Source is Visual source
                && (source is Button
                    || source is ToggleSwitch
                    || source is RangeBase
                    || source.FindAncestorOfType<Button>() is not null
                    || source.FindAncestorOfType<ToggleSwitch>() is not null
                    || source.FindAncestorOfType<RangeBase>() is not null))
            {
                return;
            }

            IsExpanded = !IsExpanded;
            args.Handled = true;
        };

        var headerGrid = new Grid
        {
            ColumnDefinitions = headerTrailing is null
                ? new ColumnDefinitions("*,Auto")
                : new ColumnDefinitions("*,Auto,Auto"),
            ColumnSpacing = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(header, 0);
        headerGrid.Children.Add(header);

        var indicatorColumn = 1;
        if (headerTrailing is not null)
        {
            Grid.SetColumn(headerTrailing, 1);
            headerGrid.Children.Add(headerTrailing);
            indicatorColumn = 2;
        }

        _indicator = new TextBlock
        {
            Width = 22,
            FontSize = 17,
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.78,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        Grid.SetColumn(_indicator, indicatorColumn);
        headerGrid.Children.Add(_indicator);

        headerRow.Child = headerGrid;
        Grid.SetRow(headerRow, 0);
        Children.Add(headerRow);

        _contentHost = new Border
        {
            Child = content,
        };
        Grid.SetRow(_contentHost, 1);
        Children.Add(_contentHost);

        _isExpanded = isExpanded;
        ApplyExpandedState();
    }

    public InstantEditorCard(
        Control header,
        IReadOnlyList<EditorInternalNavigationSection> subcards,
        EditorSubcardLayout subcardLayout,
        bool isExpanded,
        string? selectedSubcardId = null,
        Action<string>? subcardSelectionChanged = null,
        Control? footer = null,
        Control? headerTrailing = null)
        : this(
            header,
            ComposeSubcards(subcards, subcardLayout, selectedSubcardId, subcardSelectionChanged, footer),
            isExpanded,
            headerTrailing)
    {
        SubcardLayout = subcardLayout;
    }

    public EditorSubcardLayout SubcardLayout { get; }

    public event EventHandler? Expanded;
    public event Action<bool>? ExpansionChanged;

    public void SetContentSeparator(IBrush brush)
    {
        _contentHost.BorderBrush = brush;
        _contentHost.BorderThickness = new Thickness(0, 1, 0, 0);
    }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;

            _isExpanded = value;
            ApplyExpandedState();
            ExpansionChanged?.Invoke(_isExpanded);
            if (_isExpanded)
            {
                DeferredBringIntoView.Request(this);
                Expanded?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void ApplyExpandedState()
    {
        _contentHost.IsVisible = _isExpanded;
        _indicator.Text = _isExpanded ? "v" : ">";
    }

    private static Control ComposeSubcards(
        IReadOnlyList<EditorInternalNavigationSection> subcards,
        EditorSubcardLayout layout,
        string? selectedSubcardId,
        Action<string>? selectionChanged,
        Control? footer)
    {
        Control organized = new EditorSubcardLayoutHost(
            subcards, layout, selectedSubcardId, selectionChanged);
        if (footer is null) return organized;
        return new StackPanel
        {
            Spacing = EditorUiDensity.Card(10),
            Children =
            {
                organized,
                footer,
            },
        };
    }
}
