using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using System;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class InstantEditorCard : Grid
{
    private readonly TextBlock _indicator;
    private readonly ContentControl _contentHost;
    private bool _isExpanded;

    public InstantEditorCard(Control header, Control content, bool isExpanded, Control? headerTrailing = null)
    {
        RowDefinitions = new RowDefinitions("Auto,Auto");

        var headerRow = new Border
        {
            Padding = EditorUiDensity.CardThickness(12, 10),
            Cursor = new Cursor(StandardCursorType.Hand),
            Background = Brushes.Transparent,
        };
        headerRow.PointerPressed += (_, args) =>
        {
            if (args.Source is Control source
                && (source is Button
                    || source is ToggleSwitch
                    || source.FindAncestorOfType<Button>() is not null
                    || source.FindAncestorOfType<ToggleSwitch>() is not null))
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
            Width = 28,
            FontSize = 24,
            FontWeight = FontWeight.Bold,
            Opacity = 0.7,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };
        Grid.SetColumn(_indicator, indicatorColumn);
        headerGrid.Children.Add(_indicator);

        headerRow.Child = headerGrid;
        Grid.SetRow(headerRow, 0);
        Children.Add(headerRow);

        _contentHost = new ContentControl
        {
            Content = content,
        };
        Grid.SetRow(_contentHost, 1);
        Children.Add(_contentHost);

        _isExpanded = isExpanded;
        ApplyExpandedState();
    }

    public event EventHandler? Expanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;

            _isExpanded = value;
            ApplyExpandedState();
            if (_isExpanded)
            {
                DeferredBringIntoView.Request(_contentHost);
                Expanded?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void ApplyExpandedState()
    {
        _contentHost.IsVisible = _isExpanded;
        _indicator.Text = _isExpanded ? "v" : ">";
    }
}
