using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed class IconSlotsControl : StackPanel, IDictionaryValueControl
{
    private static readonly string[] ZoneKeys = ["left", "center", "right"];
    private readonly Func<string, bool, Task<string?>>? _showIconTokenPicker;
    private readonly Func<string, Control>? _createIconPreview;
    private readonly bool _isEditable;
    private Dictionary<string, List<string>> _slots = EmptySlots();
    private string _value;

    public IconSlotsControl(
        string value,
        bool isEditable,
        Func<string, bool, Task<string?>>? showIconTokenPicker,
        Func<string, Control>? createIconPreview)
    {
        _value = Normalize(value);
        _isEditable = isEditable;
        _showIconTokenPicker = showIconTokenPicker;
        _createIconPreview = createIconPreview;
        Spacing = 8;
        Rebuild();
    }

    public event EventHandler<string>? ValueChanged;

    public event EventHandler<string>? ValueCommitted;

    public string Value => _value;

    public void SetValue(string value)
    {
        var normalized = Normalize(value);
        if (_value == normalized) return;

        _value = normalized;
        Rebuild();
    }

    private void Rebuild()
    {
        _slots = Parse(_value);
        Children.Clear();
        foreach (var zone in ZoneKeys)
        {
            Children.Add(CreateZone(zone));
        }
    }

    private Control CreateZone(string zone)
    {
        var tokens = _slots[zone];
        var tokenList = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 5,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (tokens.Count == 0)
        {
            tokenList.Children.Add(new TextBlock
            {
                Text = "empty",
                Opacity = 0.55,
                VerticalAlignment = VerticalAlignment.Center,
            });
        }
        else
        {
            foreach (var token in tokens)
            {
                var preview = _createIconPreview?.Invoke(token) ?? new TextBlock { Text = token };
                tokenList.Children.Add(new Border
                {
                    CornerRadius = new CornerRadius(7),
                    Padding = new Thickness(8, 3),
                    Background = new SolidColorBrush(Color.Parse("#20314D")),
                    Child = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 5,
                        Children =
                        {
                            preview,
                            new TextBlock
                            {
                                Text = token,
                                FontSize = 12,
                                TextTrimming = TextTrimming.CharacterEllipsis,
                                MaxWidth = 92,
                                IsVisible = false,
                            },
                        },
                    },
                });
            }
        }

        var addButton = new Button
        {
            Content = "+",
            Width = 30,
            Height = 30,
            Padding = new Thickness(0),
            IsEnabled = _isEditable && _showIconTokenPicker is not null,
        };
        addButton.Click += async (_, _) =>
        {
            if (_showIconTokenPicker is null) return;

            var selected = await _showIconTokenPicker(string.Join(",", tokens), true);
            if (string.IsNullOrWhiteSpace(selected)) return;

            var next = tokens
                .Concat(selected.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where((token) => !string.IsNullOrWhiteSpace(token))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            _slots[zone] = next;
            CommitFromSlots();
        };

        var clearButton = new Button
        {
            Content = "Clear",
            MinWidth = 54,
            Height = 30,
            Padding = new Thickness(8, 0),
            IsEnabled = _isEditable && tokens.Count > 0,
        };
        clearButton.Click += (_, _) =>
        {
            _slots[zone].Clear();
            CommitFromSlots();
        };

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("74,*,Auto,Auto"),
            ColumnSpacing = 8,
            MinHeight = 36,
        };
        row.Children.Add(new TextBlock
        {
            Text = ZoneLabel(zone),
            FontWeight = FontWeight.SemiBold,
            Opacity = 0.78,
            VerticalAlignment = VerticalAlignment.Center,
        });
        Grid.SetColumn(tokenList, 1);
        row.Children.Add(tokenList);
        Grid.SetColumn(addButton, 2);
        row.Children.Add(addButton);
        Grid.SetColumn(clearButton, 3);
        row.Children.Add(clearButton);

        return new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#3D5274")),
            Child = row,
        };
    }

    private void CommitFromSlots()
    {
        _value = Serialize(_slots);
        Rebuild();
        ValueChanged?.Invoke(this, _value);
        ValueCommitted?.Invoke(this, _value);
    }

    private static string ZoneLabel(string zone)
    {
        return zone switch
        {
            "left" => "Left",
            "center" => "Center",
            "right" => "Right",
            _ => zone,
        };
    }

    private static Dictionary<string, List<string>> EmptySlots()
    {
        return ZoneKeys.ToDictionary((zone) => zone, _ => new List<string>(), StringComparer.Ordinal);
    }

    private static Dictionary<string, List<string>> Parse(string value)
    {
        var slots = EmptySlots();
        if (string.IsNullOrWhiteSpace(value)) return slots;

        try
        {
            var root = JsonNode.Parse(value)?.AsObject();
            if (root is null) return slots;
            foreach (var zone in ZoneKeys)
            {
                if (!root.TryGetPropertyValue(zone, out var node) || node is not JsonArray array) continue;
                slots[zone] = array
                    .Select((item) => item?.GetValue<string>() ?? "")
                    .Where((token) => !string.IsNullOrWhiteSpace(token))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
            }
        }
        catch (JsonException)
        {
            return slots;
        }

        return slots;
    }

    private static string Normalize(string value)
    {
        return Serialize(Parse(value));
    }

    private static string Serialize(IReadOnlyDictionary<string, List<string>> slots)
    {
        var root = new JsonObject();
        foreach (var zone in ZoneKeys)
        {
            root[zone] = new JsonArray(slots.TryGetValue(zone, out var tokens)
                ? tokens.Select((token) => JsonValue.Create(token)).ToArray<JsonNode?>()
                : []);
        }

        return root.ToJsonString();
    }
}
