using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Mockups.DesktopEditorShell.Common;
using Mockups.DesktopEditorShell.Data;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Mockups.DesktopEditorShell.EditorShell;

internal enum EditorPresentationMode
{
    Simplified,
    Complete,
}

internal sealed record EditorSimplifiedFieldReference(
    string Kind,
    string FieldId = "",
    IReadOnlyList<string>? SlotFieldIds = null,
    string CollectionFieldId = "",
    string ItemId = "",
    string ItemFieldId = "",
    IReadOnlyList<EditorSimplifiedGroupIdentity>? GroupPath = null)
{
    public static EditorSimplifiedFieldReference Direct(string fieldId) =>
        new(
            "field",
            FieldId: fieldId,
            GroupPath: [new("component", "Component", EditorIcons.General)]);

    public static EditorSimplifiedFieldReference Embedded(
        IReadOnlyList<string> slotFieldIds,
        string fieldId,
        IReadOnlyList<EditorSimplifiedGroupIdentity>? groupPath = null) =>
        new(
            "embeddedField",
            FieldId: fieldId,
            SlotFieldIds: slotFieldIds,
            GroupPath: groupPath);

    public static EditorSimplifiedFieldReference Collection(
        string collectionFieldId,
        string itemId,
        string itemFieldId,
        IReadOnlyList<EditorSimplifiedGroupIdentity>? groupPath = null,
        IReadOnlyList<string>? slotFieldIds = null) =>
        new(
            "collectionField",
            SlotFieldIds: slotFieldIds,
            CollectionFieldId: collectionFieldId,
            ItemId: itemId,
            ItemFieldId: itemFieldId,
            GroupPath: groupPath);
}

internal sealed record EditorSimplifiedGroupIdentity(string Id, string Label, string Icon);

internal sealed record EditorSimplifiedPromotion(bool IsPromoted, bool IsCaptured);

internal sealed class EditorSimplifiedProjectionState
{
    private readonly SpikeDatabase _database;
    private readonly string _recordClassId;
    private readonly EditorLayout _layout;

    public EditorSimplifiedProjectionState(
        SpikeDatabase database,
        string recordClassId,
        EditorLayout layout)
    {
        _database = database;
        _recordClassId = recordClassId;
        _layout = layout;
        CaptureEmbeddedDefaultsOnce();
    }

    public EditorSimplifiedLayout? Layout => _layout.Simplified;

    public bool IsAvailable => _layout.Simplified is not null;

    public EditorSimplifiedPromotion Promotion(EditorSimplifiedFieldReference reference)
    {
        var entry = Find(reference);
        return new EditorSimplifiedPromotion(entry?.Enabled == true, entry?.Captured == true);
    }

    public void SetPromoted(EditorSimplifiedFieldReference reference, bool promoted)
    {
        var entry = Find(reference);
        if (entry is null)
        {
            if (!promoted || _layout.Simplified is null) return;
            var group = EnsureGroup(reference.GroupPath);
            entry = CreateEntry(reference, group.Entries.Count * 10 + 10);
            group.Entries.Add(entry);
        }

        entry.Enabled = promoted;
        _database.SaveEditorLayout(_recordClassId, _layout);
    }

    private EditorSimplifiedGroup EnsureGroup(IReadOnlyList<EditorSimplifiedGroupIdentity>? path)
    {
        var normalizedPath = path is { Count: > 0 }
            ? path.Take(2).ToList()
            : [new EditorSimplifiedGroupIdentity("component", "Component", EditorIcons.General)];
        var groups = _layout.Simplified!.Groups;
        EditorSimplifiedGroup? current = null;
        foreach (var identity in normalizedPath)
        {
            current = groups.FirstOrDefault((group) => group.Id.Equals(identity.Id, StringComparison.Ordinal));
            if (current is null)
            {
                current = new EditorSimplifiedGroup
                {
                    Id = identity.Id,
                    Label = identity.Label,
                    Icon = identity.Icon,
                    Order = groups.Count * 10 + 10,
                };
                groups.Add(current);
            }
            groups = current.Groups;
        }
        return current!;
    }

    private static EditorSimplifiedEntry CreateEntry(
        EditorSimplifiedFieldReference reference,
        int order) =>
        new()
        {
            Id = string.Join(":", new[]
            {
                reference.Kind,
                reference.FieldId,
                string.Join("/", reference.SlotFieldIds ?? []),
                reference.CollectionFieldId,
                reference.ItemId,
                reference.ItemFieldId,
            }),
            Kind = reference.Kind,
            FieldId = reference.FieldId,
            SlotFieldIds = reference.SlotFieldIds?.ToList() ?? [],
            CollectionFieldId = reference.CollectionFieldId,
            ItemId = reference.ItemId,
            ItemFieldId = reference.ItemFieldId,
            Order = order,
            Enabled = true,
        };

    private EditorSimplifiedEntry? Find(EditorSimplifiedFieldReference reference) =>
        _layout.Simplified?.Entries.FirstOrDefault((entry) => Matches(entry, reference));

    private static bool Matches(
        EditorSimplifiedEntry entry,
        EditorSimplifiedFieldReference reference)
    {
        if (!entry.Kind.Equals(reference.Kind, StringComparison.Ordinal)) return false;
        return entry.Kind switch
        {
            "field" => entry.FieldId.Equals(reference.FieldId, StringComparison.Ordinal),
            "embeddedField" => entry.FieldId.Equals(reference.FieldId, StringComparison.Ordinal)
                && entry.SlotFieldIds.SequenceEqual(reference.SlotFieldIds ?? [], StringComparer.Ordinal),
            "collectionField" => entry.CollectionFieldId.Equals(reference.CollectionFieldId, StringComparison.Ordinal)
                && entry.ItemId.Equals(reference.ItemId, StringComparison.Ordinal)
                && entry.ItemFieldId.Equals(reference.ItemFieldId, StringComparison.Ordinal)
                && entry.SlotFieldIds.SequenceEqual(reference.SlotFieldIds ?? [], StringComparer.Ordinal),
            _ => false,
        };
    }

    private void CaptureEmbeddedDefaultsOnce()
    {
        var embeddedSlots = _layout.Cards
            .SelectMany((card) => card.Groups)
            .SelectMany((group) => group.Fields)
            .Select((field) => EmbeddedComponentSlotCatalog.TryGet(field.Id, out var slot) ? slot : null)
            .Where((slot) => slot is not null)
            .Cast<EmbeddedComponentSlotDefinition>()
            .DistinctBy((slot) => slot.FieldId, StringComparer.Ordinal)
            .ToList();
        var changed = false;
        foreach (var slot in embeddedSlots)
        {
            if (_layout.Simplified?.CapturedSlots.Any((capture) =>
                    capture.SlotFieldId.Equals(slot.FieldId, StringComparison.Ordinal)) == true)
            {
                continue;
            }

            var childLayout = _database.LoadEditorLayout(slot.RecordClassId);
            if (childLayout.Simplified is null) continue;
            _layout.Simplified ??= new EditorSimplifiedLayout();
            var parentGroup = _layout.Simplified.Groups.FirstOrDefault((group) =>
                group.Id.Equals(slot.FieldId, StringComparison.Ordinal));
            if (parentGroup is null)
            {
                parentGroup = new EditorSimplifiedGroup
                {
                    Id = slot.FieldId,
                    Label = slot.Label,
                    Icon = EditorIcons.Component,
                    Order = _layout.Simplified.Groups.Count * 10 + 10,
                };
                _layout.Simplified.Groups.Add(parentGroup);
            }

            foreach (var childGroup in childLayout.Simplified.Groups
                         .OrderBy((group) => group.Order)
                         .ThenBy((group) => group.Label))
            {
                var childEntries = childGroup.AllEntries()
                    .Where((entry) => entry.Enabled)
                    .OrderBy((entry) => entry.Order)
                    .ToList();
                if (childEntries.Count == 0) continue;
                var targetGroup = parentGroup.Groups.FirstOrDefault((group) =>
                    group.Id.Equals(childGroup.Id, StringComparison.Ordinal));
                if (targetGroup is null)
                {
                    targetGroup = new EditorSimplifiedGroup
                    {
                        Id = childGroup.Id,
                        Label = childGroup.Label,
                        Icon = childGroup.Icon,
                        Order = parentGroup.Groups.Count * 10 + 10,
                    };
                    parentGroup.Groups.Add(targetGroup);
                }
                foreach (var childEntry in childEntries)
                {
                    targetGroup.Entries.Add(CapturedEntry(slot, childEntry, targetGroup.Entries.Count * 10 + 10));
                }
            }
            _layout.Simplified.CapturedSlots.Add(new EditorSimplifiedCapture
            {
                SlotFieldId = slot.FieldId,
                RecordClassId = slot.RecordClassId,
            });
            changed = true;
        }
        if (changed)
        {
            _database.SaveEditorLayout(_recordClassId, _layout);
        }
    }

    private static EditorSimplifiedEntry CapturedEntry(
        EmbeddedComponentSlotDefinition parentSlot,
        EditorSimplifiedEntry child,
        int order)
    {
        var slots = new List<string> { parentSlot.FieldId };
        slots.AddRange(child.SlotFieldIds);
        return new EditorSimplifiedEntry
        {
            Id = $"captured:{parentSlot.FieldId}:{child.Id}",
            Kind = child.Kind == "field" ? "embeddedField" : child.Kind,
            FieldId = child.FieldId,
            SlotFieldIds = slots,
            CollectionFieldId = child.CollectionFieldId,
            ItemId = child.ItemId,
            ItemFieldId = child.ItemFieldId,
            Order = order,
            Enabled = true,
            Captured = true,
        };
    }
}

internal static class EditorSimplifiedPromotionControl
{
    public static Control Wrap(
        Control field,
        EditorSimplifiedProjectionState? projection,
        EditorSimplifiedFieldReference reference)
    {
        if (projection is null || !projection.IsAvailable) return field;

        var promotion = projection.Promotion(reference);
        var checkbox = new CheckBox
        {
            IsChecked = promotion.IsPromoted,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 8, 0, 0),
        };
        EditorAccessibility.Describe(
            checkbox,
            promotion.IsPromoted ? "Remove from Simplified editor" : "Show in Simplified editor");
        ToolTip.SetTip(
            checkbox,
            promotion.IsCaptured
                ? "Captured from the embedded component · editable in this parent"
                : promotion.IsPromoted ? "Shown in Simplified" : "Show in Simplified");
        checkbox.PropertyChanged += (_, change) =>
        {
            if (change.Property != ToggleButton.IsCheckedProperty) return;
            projection.SetPromoted(reference, checkbox.IsChecked == true);
        };

        var marker = promotion.IsCaptured
            ? EditorIcons.Create(EditorIcons.Lock, 12)
            : null;
        if (marker is not null)
        {
            marker.Opacity = 0.68;
            ToolTip.SetTip(marker, "Captured default; changes are local to this parent");
        }

        var indicator = new StackPanel
        {
            Width = 22,
            Spacing = 2,
            HorizontalAlignment = HorizontalAlignment.Left,
            Children = { checkbox },
        };
        if (marker is not null) indicator.Children.Add(marker);

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("22,*"),
            ColumnSpacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        row.Children.Add(indicator);
        Grid.SetColumn(field, 1);
        row.Children.Add(field);
        return row;
    }

    public static Control WrapCaptured(Control field)
    {
        var marker = EditorIcons.Create(EditorIcons.Lock, 12);
        marker.Opacity = 0.68;
        marker.VerticalAlignment = VerticalAlignment.Top;
        marker.Margin = new Thickness(0, 10, 0, 0);
        ToolTip.SetTip(marker, "Captured default; changes are local to this parent");
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("16,*"),
            ColumnSpacing = 6,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Children = { marker },
        };
        Grid.SetColumn(field, 1);
        row.Children.Add(field);
        return row;
    }
}

internal static class EditorPresentationModeSelector
{
    public static Control Create(EditorPresentationMode selected, Action<EditorPresentationMode> select)
    {
        var simplified = Button("Simplified", selected == EditorPresentationMode.Simplified);
        var complete = Button("Complete", selected == EditorPresentationMode.Complete);
        simplified.Click += (_, _) => select(EditorPresentationMode.Simplified);
        complete.Click += (_, _) => select(EditorPresentationMode.Complete);

        var selector = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { simplified, complete },
        };
        return new Grid
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = EditorUiDensity.CardThickness(0, 0, 0, 2),
            Children =
            {
                new Border
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    CornerRadius = new CornerRadius(8),
                    Padding = new Thickness(2),
                    Background = new SolidColorBrush(Color.FromArgb(28, 127, 127, 127)),
                    Child = selector,
                },
            },
        };
    }

    private static Button Button(string label, bool selected)
    {
        var button = new Button
        {
            Content = label,
            MinWidth = 92,
            Height = 30,
            Padding = new Thickness(12, 0),
            FontSize = 12,
            FontWeight = selected ? FontWeight.SemiBold : FontWeight.Normal,
            Background = selected
                ? new SolidColorBrush(Color.FromArgb(72, 90, 145, 255))
                : Brushes.Transparent,
            BorderThickness = new Thickness(0),
        };
        return button;
    }
}
