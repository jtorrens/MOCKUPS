using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class StatusBarItemsCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly bool _isDark;
    private readonly Func<string, ValueKind, Task<string?>> _browsePath;
    private readonly EditorDictionaryFieldServices _dictionaryServices;
    private readonly Action _onChanged;

    public StatusBarItemsCollectionEditor(
        SpikeDatabase database,
        bool isDark,
        Func<string, ValueKind, Task<string?>> browsePath,
        EditorDictionaryFieldServices dictionaryServices,
        Action onChanged)
    {
        _database = database;
        _isDark = isDark;
        _browsePath = browsePath;
        _dictionaryServices = dictionaryServices;
        _onChanged = onChanged;
    }

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        var icon = EditorIcons.CreateSemantic("Items", EditorIcons.Status, 18);
        var items = Items(node).ToList();
        var body = new StackPanel
        {
            Spacing = 10,
        };

        body.Children.Add(new TextBlock
        {
            Text = "Status items resolve left/right zones by order. Icon rows use semantic icon tokens from System Data → Icon Themes.",
            TextWrapping = TextWrapping.Wrap,
            Opacity = 0.72,
        });

        for (var index = 0; index < items.Count; index++)
        {
            body.Children.Add(CreateItemRow(node, index, items[index]));
        }

        return new InstantEditorCard(
            EditorCardHeader.Create("Items", $"{items.Count} status items", icon),
            new Border
            {
                Padding = new Thickness(10),
                Child = body,
            },
            isExpanded: false)
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            SessionStateId = "collection:status-bar-items",
        };
    }

    private Control CreateItemRow(ProjectTreeNode node, int index, SpikeDatabase.StatusBarItem item)
    {
        var row = new Border
        {
            Padding = new Thickness(10),
            CornerRadius = new CornerRadius(10),
            BorderBrush = new SolidColorBrush(Color.Parse(_isDark ? "#34445A" : "#D0D7E2")),
            BorderThickness = new Thickness(1),
        };
        var panel = new StackPanel
        {
            Spacing = 8,
        };
        row.Child = panel;

        panel.Children.Add(new TextBlock
        {
            Text = $"{item.Label} · {item.Kind}",
            FontWeight = FontWeight.SemiBold,
        });

        var controlsPanel = new StackPanel
        {
            Spacing = 8,
        };

        var valueControl = item.Kind switch
        {
            "iconToken" => CreateIconTokenControl(node, index, item),
            "generatedBattery" => CreateGeneratedControl(node, index, item, includeCharging: true),
            "generatedSignal" => CreateGeneratedControl(node, index, item, includeCharging: false),
            _ => CreateTextControl(node, index, item),
        };
        controlsPanel.Children.Add(valueControl);

        controlsPanel.Children.Add(CreateInlineField(
            new FieldValue(
                new FieldDefinition(
                    $"statusBar.items.{index}.zone",
                    "Zone",
                    ValueKind.OptionToken,
                    DefaultValue: item.Zone,
                    Options:
                    [
                        new FieldOption("off", "Off"),
                        new FieldOption("left", "Left"),
                        new FieldOption("right", "Right"),
                    ]),
                item.Zone),
            (value) => UpdateItem(node, index, (current) => current with { Zone = value })));

        controlsPanel.Children.Add(CreateInlineField(
            new FieldValue(
                new FieldDefinition(
                    $"statusBar.items.{index}.order",
                    "Order",
                    ValueKind.Integer,
                    DefaultValue: item.Order.ToString()),
                item.Order.ToString()),
            (value) => UpdateItem(node, index, (current) => current with { Order = NumericText.Int32(value, current.Order) })));

        panel.Children.Add(controlsPanel);
        return row;
    }

    private Control CreateTextControl(ProjectTreeNode node, int index, SpikeDatabase.StatusBarItem item)
    {
        return CreateInlineField(
            new FieldValue(
                new FieldDefinition(
                    $"statusBar.items.{index}.value",
                    "Value",
                    ValueKind.StringSingleLine,
                    DefaultValue: item.Value),
                item.Value),
            (value) => UpdateItem(node, index, (current) => current with { Value = value }));
    }

    private Control CreateIconTokenControl(ProjectTreeNode node, int index, SpikeDatabase.StatusBarItem item)
    {
        return CreateInlineField(
            new FieldValue(
                new FieldDefinition(
                    $"statusBar.items.{index}.token",
                    "Icon token",
                    ValueKind.IconToken,
                    DefaultValue: item.Token),
                item.Token),
            (value) => UpdateItem(node, index, (current) => current with { Token = value }),
            _dictionaryServices.ForNode(node, (_) => ""));
    }

    private Control CreateGeneratedControl(ProjectTreeNode node, int index, SpikeDatabase.StatusBarItem item, bool includeCharging)
    {
        var grid = new Grid
        {
            ColumnDefinitions = includeCharging ? new ColumnDefinitions("*,120") : new ColumnDefinitions("*"),
            ColumnSpacing = 10,
        };
        grid.Children.Add(CreateInlineField(
            new FieldValue(
                new FieldDefinition(
                    $"statusBar.items.{index}.value",
                    item.Kind == "generatedBattery" ? "Battery %" : "Signal",
                    ValueKind.Integer,
                    DefaultValue: item.Value),
                item.Value),
            (value) => UpdateItem(node, index, (current) => current with { Value = value })));

        if (includeCharging)
        {
            var charging = CreateInlineField(
                new FieldValue(
                    new FieldDefinition(
                        $"statusBar.items.{index}.charging",
                        "Charging",
                        ValueKind.Boolean,
                        DefaultValue: BoolToString(item.Charging)),
                    BoolToString(item.Charging)),
                (value) => UpdateItem(node, index, (current) => current with { Charging = StringToBool(value) }));
            Grid.SetColumn(charging, 1);
            grid.Children.Add(charging);
        }

        return grid;
    }

    private DictionaryFieldControl CreateInlineField(
        FieldValue fieldValue,
        Action<string> persist,
        DictionaryFieldServices? services = null)
    {
        services ??= new DictionaryFieldServices(BrowsePath: _browsePath);
        var control = new DictionaryFieldControl(fieldValue, services);
        control.ValueCommitted += (_, value) => persist(value);
        return control;
    }

    private void UpdateItem(ProjectTreeNode node, int index, Func<SpikeDatabase.StatusBarItem, SpikeDatabase.StatusBarItem> patch)
    {
        var current = Items(node).ElementAtOrDefault(index);
        if (current is null)
        {
            return;
        }

        var nextItem = patch(current);
        _database.UpdateStatusBarComponentItem(node.Id, index, nextItem);
        _onChanged();
    }

    private IReadOnlyList<SpikeDatabase.StatusBarItem> Items(ProjectTreeNode node) =>
        _database.GetStatusBarComponentItems(node.Id);

    private static string BoolToString(bool value) => BooleanText.Format(value);

    private static bool StringToBool(string value) =>
        BooleanText.Parse(value);
}
