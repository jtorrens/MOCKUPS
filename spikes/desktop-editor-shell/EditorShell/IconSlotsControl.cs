using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
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
    private readonly Func<string, Task>? _openComponentVariantReference;
    private readonly Func<string, JsonObject, Action<JsonObject>, Task>? _openRuntimeComponentOverrides;
    private readonly bool _isEditable;
    private readonly FixedComponentVariantBoundary _buttonBoundary;
    private List<JsonObject> _items = [];
    private int _selectedIndex;
    private string _value;

    public IconSlotsControl(
        string value,
        bool isEditable,
        Func<string, bool, Task<string?>>? showIconTokenPicker,
        Func<string, Control>? createIconPreview,
        IReadOnlyList<FieldOption> buttonVariantOptions,
        Func<string, Task>? openComponentVariantReference,
        Func<string, JsonObject, Action<JsonObject>, Task>? openRuntimeComponentOverrides)
    {
        _isEditable = isEditable;
        _showIconTokenPicker = showIconTokenPicker;
        _createIconPreview = createIconPreview;
        _buttonBoundary = ComponentVariantOptionContract.RequireFixedBoundary(
            buttonVariantOptions,
            "Icon Slots Button boundary");
        _openComponentVariantReference = openComponentVariantReference;
        _openRuntimeComponentOverrides = openRuntimeComponentOverrides;
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
            return BuildFirstSlotCreator();
        }
        return new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Content = slots };
    }

    private Control BuildFirstSlotCreator()
    {
        var panel = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            ColumnSpacing = 8,
        };
        panel.Children.Add(new TextBlock
        {
            Text = "No Buttons.",
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var add = ActionButton(
            EditorIcons.Add,
            "Create first Button",
            _isEditable,
            AddFirstItem);
        Grid.SetColumn(add, 1);
        panel.Children.Add(add);
        return panel;
    }

    private Control BuildSelectedEditor()
    {
        var item = _items[_selectedIndex];
        var editor = new StackPanel { Spacing = 8 };

        var buttonSlot = ComponentVariantSlotDocumentContract.Create(
            String(item, "buttonVariantReference", ""),
            item["buttonOverrides"]?.AsObject()?.DeepClone().AsObject()
            ?? throw new InvalidOperationException("Icon Row Button item requires local Overrides."),
            $"Icon Row Button '{String(item, "id", "")}'");
        var buttonVariant = new DictionaryComponentVariantSlotControl(
            new FieldDefinition(
                $"iconSlots.{String(item, "id", "")}.buttonSlot",
                "Button Variant",
                ValueKind.ComponentVariantSlot,
                _isEditable,
                Options: _buttonBoundary.VariantOptions,
                SelectComponentClass: false),
            buttonSlot.ToJsonString(),
            _openComponentVariantReference,
            _openRuntimeComponentOverrides);
        buttonVariant.ValueCommitted += (_, value) =>
        {
            var owner = $"Icon Row Button '{String(item, "id", "")}'";
            var next = ComponentVariantSlotDocumentContract.Parse(value, owner);
            item["buttonVariantReference"] = ComponentVariantSlotDocumentContract.VariantReference(next, owner);
            item["buttonOverrides"] = ComponentVariantSlotDocumentContract.Overrides(next, owner).DeepClone();
            Commit();
        };
        editor.Children.Add(FieldRow("Button", buttonVariant));

        var state = OptionControl(
            String(item, "state", "normal"),
            [("normal", "Normal"), ("active", "Active"), ("pushed", "Pushed"), ("disabled", "Disabled")],
            (value) => { item["state"] = value; Commit(); });
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
        var preview = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 4,
        };
        preview.Children.Add(
            _createIconPreview?.Invoke(String(item, "iconToken", ""))
            ?? new TextBlock { Text = "•" });
        var text = String(item, "text", "");
        if (!string.IsNullOrWhiteSpace(text))
        {
            preview.Children.Add(new TextBlock
            {
                Text = text,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });
        }
        return preview;
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
        if (_selectedIndex < 0) return;
        var index = _selectedIndex + 1;
        _items.Insert(index, NewItem(_items[_selectedIndex]));
        _selectedIndex = index;
        Commit();
    }

    private void AddFirstItem()
    {
        _items.Add(NewItem(null));
        _selectedIndex = 0;
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
        item["buttonVariantReference"] = template?["buttonVariantReference"]?.GetValue<string>()
            ?? _buttonBoundary.DefaultVariantReference;
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
