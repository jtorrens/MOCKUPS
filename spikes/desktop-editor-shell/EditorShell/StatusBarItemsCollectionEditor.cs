using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class StatusBarItemsCollectionEditor
{
    private readonly SpikeDatabase _database;
    private readonly bool _isDark;
    private readonly Func<string, ValueKind, Task<string?>> _browsePath;
    private readonly Func<string, string, bool, Task<string?>> _showIconTokenPicker;
    private readonly Action _onChanged;

    public StatusBarItemsCollectionEditor(
        SpikeDatabase database,
        bool isDark,
        Func<string, ValueKind, Task<string?>> browsePath,
        Func<string, string, bool, Task<string?>> showIconTokenPicker,
        Action onChanged)
    {
        _database = database;
        _isDark = isDark;
        _browsePath = browsePath;
        _showIconTokenPicker = showIconTokenPicker;
        _onChanged = onChanged;
    }

    public InstantEditorCard Create(ProjectTreeNode node)
    {
        var icon = EditorIcons.Create(EditorIcons.Status, 18);
        var settings = _database.GetStatusBarSettings(node.Id);
        var items = _database.GetStatusBarItems(node.Id).ToList();
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
            body.Children.Add(CreateItemRow(node, settings.ProjectId, index, items[index]));
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
        };
    }

    private Control CreateItemRow(ProjectTreeNode node, string projectId, int index, SpikeDatabase.StatusBarItem item)
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
            "iconToken" => CreateIconTokenControl(node, projectId, index, item),
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
            (value) => UpdateItem(node, index, item with { Zone = value })));

        controlsPanel.Children.Add(CreateInlineField(
            new FieldValue(
                new FieldDefinition(
                    $"statusBar.items.{index}.order",
                    "Order",
                    ValueKind.Integer,
                    DefaultValue: item.Order.ToString()),
                item.Order.ToString()),
            (value) => UpdateItem(node, index, item with { Order = int.TryParse(value, out var parsed) ? parsed : item.Order })));

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
            (value) => UpdateItem(node, index, item with { Value = value }));
    }

    private Control CreateIconTokenControl(ProjectTreeNode node, string projectId, int index, SpikeDatabase.StatusBarItem item)
    {
        var currentItem = item;
        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("38,*,Auto"),
            ColumnSpacing = 8,
        };
        var previewBox = new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(8),
            BorderBrush = new SolidColorBrush(Color.Parse("#4B5B75")),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = SvgIconPreview.CreateProjectIconTokenPreview(_database, projectId, item.Token, 21),
        };
        grid.Children.Add(previewBox);

        var tokenBox = new TextBox
        {
            Text = item.Token,
            IsReadOnly = true,
            MinHeight = 36,
            PlaceholderText = "Select icon token…",
            VerticalContentAlignment = VerticalAlignment.Center,
        };
        EditorTextBoxBehavior.Configure(tokenBox);
        Grid.SetColumn(tokenBox, 1);
        grid.Children.Add(tokenBox);

        var pickButton = new Button
        {
            Content = "Pick…",
            MinWidth = 72,
            VerticalAlignment = VerticalAlignment.Center,
        };
        pickButton.Click += async (_, _) =>
        {
            var selected = await _showIconTokenPicker(projectId, currentItem.Token, false);
            if (!string.IsNullOrWhiteSpace(selected))
            {
                currentItem = currentItem with { Token = selected };
                UpdateItem(node, index, currentItem);
                tokenBox.Text = selected;
                previewBox.Child = SvgIconPreview.CreateProjectIconTokenPreview(_database, projectId, selected, 21);
            }
        };
        Grid.SetColumn(pickButton, 2);
        grid.Children.Add(pickButton);
        return grid;
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
            (value) => UpdateItem(node, index, item with { Value = value })));

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
                (value) => UpdateItem(node, index, item with { Charging = StringToBool(value) }));
            Grid.SetColumn(charging, 1);
            grid.Children.Add(charging);
        }

        return grid;
    }

    private DictionaryFieldControl CreateInlineField(FieldValue fieldValue, Action<string> persist)
    {
        var control = new DictionaryFieldControl(fieldValue, _browsePath);
        control.ValueCommitted += (_, value) => persist(value);
        return control;
    }

    private void UpdateItem(ProjectTreeNode node, int index, SpikeDatabase.StatusBarItem nextItem)
    {
        _database.UpdateStatusBarItem(node.Id, index, nextItem);
        _onChanged();
    }

    private static string BoolToString(bool value) => value ? "true" : "false";

    private static bool StringToBool(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1";
}
