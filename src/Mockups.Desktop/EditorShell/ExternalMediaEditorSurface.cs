using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class ExternalMediaEditorSurface
{
    private readonly IExternalMediaUsageQuery _usage;
    private readonly EditorOperationCoordinator _operations;
    private readonly Func<bool> _isDark;
    private readonly Func<ExternalMediaUsageDetail, Task> _navigate;

    public ExternalMediaEditorSurface(
        IExternalMediaUsageQuery usage,
        EditorOperationCoordinator operations,
        Func<bool> isDark,
        Func<ExternalMediaUsageDetail, Task> navigate)
    {
        _usage = usage;
        _operations = operations;
        _isDark = isDark;
        _navigate = navigate;
    }

    public IReadOnlyList<InstantEditorCard>? CreateCards(ProjectTreeNode node)
    {
        if (node.Kind != ProjectTreeNodeKind.ExternalMediaRoot)
        {
            return null;
        }
        var project = node.Parent is { Kind: ProjectTreeNodeKind.Project } owner
            ? owner
            : throw new InvalidOperationException(
                $"External Media '{node.Id}' has no Project owner.");
        return
        [
            DeferredEditorCard.Create(
                "External Media",
                "Loading authored external dependencies",
                () => EditorIcons.Create(EditorIcons.Media, 18),
                "external-media:inventory",
                (cancellationToken) => _operations.ExecuteAsync(
                    () => _usage.GetExternalMediaUsageDetails(project.Id),
                    cancellationToken),
                (items) => new DeferredEditorCardContent(
                    items.Count == 1
                        ? "1 authored dependency"
                        : $"{items.Count} authored dependencies",
                    new ExternalMediaTableControl(
                        items,
                        _isDark(),
                        _navigate)),
                isExpanded: true),
        ];
    }
}

internal sealed class ExternalMediaTableControl : StackPanel
{
    private readonly IReadOnlyList<ExternalMediaUsageDetail> _items;
    private readonly bool _isDark;
    private readonly Func<ExternalMediaUsageDetail, Task> _navigate;
    private readonly StackPanel _rows;
    private readonly Dictionary<ExternalMediaSortColumn, Button> _headers = [];
    private ExternalMediaSortColumn _sortColumn = ExternalMediaSortColumn.SystemItem;
    private bool _descending;

    public ExternalMediaTableControl(
        IReadOnlyList<ExternalMediaUsageDetail> items,
        bool isDark,
        Func<ExternalMediaUsageDetail, Task> navigate)
    {
        _items = items;
        _isDark = isDark;
        _navigate = navigate;
        Name = "ExternalMediaTable";
        Spacing = 0;

        var header = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2.2*,3*,1.25*"),
            ColumnSpacing = 12,
        };
        AddHeader(header, ExternalMediaSortColumn.SystemItem, "System item", 0);
        AddHeader(header, ExternalMediaSortColumn.AbsolutePath, "Absolute path", 1);
        AddHeader(header, ExternalMediaSortColumn.FileName, "File name", 2);
        Children.Add(new Border
        {
            Padding = new Thickness(8, 5),
            BorderBrush = EditorUiVisuals.ConnectorBrush(_isDark),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = header,
        });

        _rows = new StackPanel { Spacing = 0 };
        Children.Add(_rows);
        Rebuild();
    }

    private void AddHeader(
        Grid host,
        ExternalMediaSortColumn column,
        string label,
        int gridColumn)
    {
        var button = new Button
        {
            Name = column switch
            {
                ExternalMediaSortColumn.SystemItem => "ExternalMediaSystemItemHeader",
                ExternalMediaSortColumn.AbsolutePath => "ExternalMediaAbsolutePathHeader",
                ExternalMediaSortColumn.FileName => "ExternalMediaFileNameHeader",
                _ => throw new InvalidOperationException(
                    $"Unknown External Media sort column '{column}'."),
            },
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(3, 4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        button.Click += (_, _) => Sort(column);
        Grid.SetColumn(button, gridColumn);
        host.Children.Add(button);
        _headers[column] = button;
        UpdateHeader(button, column, label);
    }

    private void Sort(ExternalMediaSortColumn column)
    {
        if (_sortColumn == column)
        {
            _descending = !_descending;
        }
        else
        {
            _sortColumn = column;
            _descending = false;
        }
        foreach (var (candidate, button) in _headers)
        {
            var label = candidate switch
            {
                ExternalMediaSortColumn.SystemItem => "System item",
                ExternalMediaSortColumn.AbsolutePath => "Absolute path",
                ExternalMediaSortColumn.FileName => "File name",
                _ => throw new InvalidOperationException(
                    $"Unknown External Media sort column '{candidate}'."),
            };
            UpdateHeader(button, candidate, label);
        }
        Rebuild();
    }

    private void UpdateHeader(
        Button button,
        ExternalMediaSortColumn column,
        string label)
    {
        button.Content = new TextBlock
        {
            Text = _sortColumn == column
                ? $"{label} {(_descending ? "↓" : "↑")}"
                : label,
            FontWeight = FontWeight.SemiBold,
            FontSize = 12,
        };
    }

    private void Rebuild()
    {
        _rows.Children.Clear();
        if (_items.Count == 0)
        {
            _rows.Children.Add(new TextBlock
            {
                Text = "No authored external media references were found.",
                Margin = new Thickness(10),
                Opacity = 0.68,
            });
            return;
        }

        IEnumerable<ExternalMediaUsageDetail> ordered = _sortColumn switch
        {
            ExternalMediaSortColumn.SystemItem => _items.OrderBy(
                (item) => item.SystemItem,
                StringComparer.OrdinalIgnoreCase),
            ExternalMediaSortColumn.AbsolutePath => _items.OrderBy(
                (item) => item.AbsoluteDirectoryPath,
                StringComparer.OrdinalIgnoreCase),
            ExternalMediaSortColumn.FileName => _items.OrderBy(
                (item) => item.FileName,
                StringComparer.OrdinalIgnoreCase),
            _ => throw new InvalidOperationException(
                $"Unknown External Media sort column '{_sortColumn}'."),
        };
        if (_descending) ordered = ordered.Reverse();
        foreach (var item in ordered)
        {
            _rows.Children.Add(CreateRow(item));
        }
    }

    private Control CreateRow(ExternalMediaUsageDetail item)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("2.2*,3*,1.25*"),
            ColumnSpacing = 12,
        };
        var systemItem = new Button
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(3, 4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            Content = new TextBlock
            {
                Text = item.SystemItem,
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextDecorations = TextDecorations.Underline,
                Foreground = new SolidColorBrush(
                    Color.Parse(_isDark ? "#D6A638" : "#A56600")),
            },
        };
        systemItem.Click += async (_, _) => await _navigate(item);
        ToolTip.SetTip(systemItem, $"Open {item.SystemItem}");
        Grid.SetColumn(systemItem, 0);
        row.Children.Add(systemItem);

        var path = CellText(item.AbsoluteDirectoryPath, item.Exists, _isDark);
        path.Cursor = item.Exists
            ? new Cursor(StandardCursorType.Hand)
            : Cursor.Default;
        path.PointerPressed += (_, args) =>
        {
            if (!item.Exists
                || args.GetCurrentPoint(path).Properties.PointerUpdateKind
                    != PointerUpdateKind.RightButtonPressed)
            {
                return;
            }
            EditorLocalPathActions.Reveal(item.AbsoluteTargetPath);
            args.Handled = true;
        };
        ToolTip.SetTip(
            path,
            item.Exists
                ? $"Right-click to show in Finder\n{item.AbsoluteTargetPath}"
                : $"Missing\n{item.AbsoluteTargetPath}");
        Grid.SetColumn(path, 1);
        row.Children.Add(path);

        var fileName = CellText(
            item.Exists ? item.FileName : $"{item.FileName} · Missing",
            item.Exists,
            _isDark);
        Grid.SetColumn(fileName, 2);
        row.Children.Add(fileName);

        return new Border
        {
            Padding = new Thickness(8, 4),
            BorderBrush = EditorUiVisuals.ConnectorBrush(_isDark),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = row,
        };
    }

    private static TextBlock CellText(
        string text,
        bool exists,
        bool isDark) => new()
    {
        Text = text,
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        Opacity = exists ? 0.82 : 1,
        Foreground = exists
            ? EditorUiVisuals.SecondaryTextBrush(isDark)
            : new SolidColorBrush(Color.Parse("#E06C75")),
    };

    private enum ExternalMediaSortColumn
    {
        SystemItem,
        AbsolutePath,
        FileName,
    }
}
