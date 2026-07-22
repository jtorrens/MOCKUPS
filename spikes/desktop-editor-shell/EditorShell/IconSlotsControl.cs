using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Mockups.DesktopEditorShell.EditorShell;

// Edits the ordered Button runtime collection owned by one Icon Row.
internal sealed class IconSlotsControl : StackPanel, IDictionaryValueControl
{
    private readonly Func<string, bool, Task<string?>>? _showIconTokenPicker;
    private readonly Func<string, Control>? _createIconPreview;
    private readonly bool _isEditable;
    private readonly string _defaultButtonVariantReference;
    private List<JsonObject> _items = [];
    private int _selectedIndex;
    private string _value;

    public IconSlotsControl(
        string value,
        bool isEditable,
        Func<string, bool, Task<string?>>? showIconTokenPicker,
        Func<string, Control>? createIconPreview,
        string defaultButtonVariantReference)
    {
        _isEditable = isEditable;
        _showIconTokenPicker = showIconTokenPicker;
        _createIconPreview = createIconPreview;
        _defaultButtonVariantReference = defaultButtonVariantReference;
        _value = Normalize(value);
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
        _items = Parse(_value);
        _selectedIndex = _items.Count == 0 ? -1 : Math.Clamp(_selectedIndex, 0, _items.Count - 1);
        Children.Clear();
        Children.Add(BuildSlotStrip());
        if (_selectedIndex >= 0) Children.Add(BuildSelectedEditor());
    }

    private Control BuildSlotStrip()
    {
        var slots = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        for (var index = 0; index < _items.Count; index++)
        {
            var captured = index;
            var item = _items[index];
            var button = new Button
            {
                Width = 38,
                Height = 34,
                Padding = new Thickness(4),
                BorderThickness = new Thickness(index == _selectedIndex ? 2 : 1),
                BorderBrush = new SolidColorBrush(Color.Parse(index == _selectedIndex ? "#D6A638" : "#4B5F7A")),
                Content = SlotPreview(item),
            };
            button.Click += (_, _) => { _selectedIndex = captured; Rebuild(); };
            slots.Children.Add(button);
        }

        if (_items.Count == 0)
        {
            slots.Children.Add(ActionButton(EditorIcons.Add, "Create first slot", _isEditable, InsertAfterSelection));
        }
        return new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = slots };
    }

    private Control BuildSelectedEditor()
    {
        var item = _items[_selectedIndex];
        var editor = new StackPanel { Spacing = 8 };

        var contentMode = OptionControl(
            String(item, "contentMode", "icon"),
            [("icon", "Icon"), ("text", "Text"), ("iconText", "Icon + text")],
            (value) => { item["contentMode"] = value; Commit(); });
        var state = OptionControl(
            String(item, "state", "normal"),
            [("normal", "Normal"), ("active", "Active"), ("pushed", "Pushed"), ("disabled", "Disabled")],
            (value) => { item["state"] = value; Commit(); });
        editor.Children.Add(FieldRow("Content", contentMode));
        editor.Children.Add(FieldRow("State", state));

        var iconRow = new Grid { ColumnDefinitions = new ColumnDefinitions("38,*,Auto"), ColumnSpacing = 8 };
        iconRow.Children.Add(new Border
        {
            Width = 34,
            Height = 34,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#4B5F7A")),
            Child = _createIconPreview?.Invoke(String(item, "iconToken", "")),
        });
        var iconName = new TextBlock
        {
            Text = String(item, "iconToken", "Select icon…"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(iconName, 1);
        iconRow.Children.Add(iconName);
        var pick = new Button { Content = "Pick…", MinWidth = 68, IsEnabled = _isEditable && _showIconTokenPicker is not null };
        pick.Click += async (_, _) =>
        {
            if (_showIconTokenPicker is null) return;
            var selected = await _showIconTokenPicker(String(item, "iconToken", ""), false);
            if (string.IsNullOrWhiteSpace(selected)) return;
            item["iconToken"] = selected;
            Commit();
        };
        Grid.SetColumn(pick, 2);
        iconRow.Children.Add(pick);
        editor.Children.Add(FieldRow("Icon", iconRow));

        var label = EditorTextBoxBehavior.Configure(new TextBox
        {
            Text = String(item, "text", ""),
            IsEnabled = _isEditable,
            PlaceholderText = "Label",
        });
        label.LostFocus += (_, _) =>
        {
            if (String(item, "text", "") == label.Text) return;
            item["text"] = label.Text ?? "";
            Commit();
        };
        editor.Children.Add(FieldRow("Label", label));

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, HorizontalAlignment = HorizontalAlignment.Right };
        actions.Children.Add(ActionButton(EditorIcons.Left, "Move left", _isEditable && _selectedIndex > 0, () => Move(-1)));
        actions.Children.Add(ActionButton(EditorIcons.Right, "Move right", _isEditable && _selectedIndex < _items.Count - 1, () => Move(1)));
        actions.Children.Add(ActionButton(EditorIcons.Add, "Insert after", _isEditable, InsertAfterSelection));
        actions.Children.Add(ActionButton(EditorIcons.Delete, "Delete selected slot", _isEditable, DeleteSelection));
        editor.Children.Add(actions);

        return new Border
        {
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(10),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#3D5274")),
            Child = editor,
        };
    }

    private Control SlotPreview(JsonObject item)
    {
        var mode = String(item, "contentMode", "icon");
        if (mode == "text") return new TextBlock { Text = String(item, "text", "T"), TextTrimming = TextTrimming.CharacterEllipsis };
        return _createIconPreview?.Invoke(String(item, "iconToken", "")) ?? new TextBlock { Text = "•" };
    }

    private static Control FieldRow(string label, Control control)
    {
        var row = new Grid { ColumnDefinitions = new ColumnDefinitions("72,*"), ColumnSpacing = 8 };
        row.Children.Add(new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center, Opacity = 0.76 });
        Grid.SetColumn(control, 1);
        row.Children.Add(control);
        return row;
    }

    private static Control OptionControl(string value, (string Value, string Label)[] options, Action<string> changed)
    {
        var items = options.Select((option) => new FieldOption(option.Value, option.Label)).ToList();
        var combo = new EditorInstantComboBox { ItemsSource = items, SelectedItem = items.First((option) => option.Value == value) };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is FieldOption option) changed(option.Value);
        };
        return combo;
    }

    private static Button ActionButton(string icon, string tooltip, bool enabled, Action action)
    {
        var button = new Button { Content = EditorIcons.CreateSemantic(tooltip, icon, 15), Width = 32, Height = 30, Padding = new Thickness(0), IsEnabled = enabled };
        ToolTip.SetTip(button, tooltip);
        button.Click += (_, _) => action();
        return button;
    }

    private void Move(int offset)
    {
        var next = _selectedIndex + offset;
        if (next < 0 || next >= _items.Count) return;
        (_items[_selectedIndex], _items[next]) = (_items[next], _items[_selectedIndex]);
        _selectedIndex = next;
        Commit();
    }

    private void InsertAfterSelection()
    {
        var index = _selectedIndex < 0 ? 0 : _selectedIndex + 1;
        _items.Insert(index, NewItem(_selectedIndex >= 0 ? _items[_selectedIndex] : null));
        _selectedIndex = index;
        Commit();
    }

    private void DeleteSelection()
    {
        if (_selectedIndex < 0) return;
        _items.RemoveAt(_selectedIndex);
        _selectedIndex = _items.Count == 0 ? -1 : Math.Min(_selectedIndex, _items.Count - 1);
        Commit();
    }

    private void Commit()
    {
        _value = Serialize(_items);
        Rebuild();
        ValueChanged?.Invoke(this, _value);
        ValueCommitted?.Invoke(this, _value);
    }

    private JsonObject NewItem(JsonObject? template)
    {
        var item = template is null ? new JsonObject() : Clone(template);
        item["id"] = $"button_{Guid.NewGuid():N}";
        item["buttonVariantReference"] = template?["buttonVariantReference"]?.GetValue<string>() ?? _defaultButtonVariantReference;
        item["contentMode"] ??= "icon";
        item["state"] ??= "normal";
        item["iconToken"] ??= "media_mic";
        item["text"] ??= "";
        item["iconSizeToken"] ??= "theme.iconSizes.m";
        item["textSizeToken"] ??= "theme.typography.sizes.s";
        item["pushTrigger"] = false;
        item["pushElapsedMs"] = 0;
        item["buttonOverrides"] ??= new JsonObject();
        return item;
    }

    private static string String(JsonObject item, string key, string fallback) => item[key]?.GetValue<string>() ?? fallback;
    private static List<JsonObject> Parse(string value)
    {
        var items = RuntimeInputValueKindContract.ParseValue(
            ValueKind.IconSlots,
            value,
            "Icon Slots value").AsArray();
        return items.Select((item) => Clone(item!.AsObject())).ToList();
    }
    private static JsonObject Clone(JsonObject value) => value.DeepClone().AsObject();
    private static string Normalize(string value) => Serialize(Parse(value));
    private static string Serialize(IEnumerable<JsonObject> items) => new JsonArray(items.Select((item) => (JsonNode?)Clone(item)).ToArray()).ToJsonString();
}
