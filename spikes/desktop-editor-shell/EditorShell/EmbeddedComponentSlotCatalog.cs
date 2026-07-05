using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

internal sealed record EmbeddedComponentSlotDefinition(
    string FieldId,
    string EmbeddedComponentType,
    string Label,
    string RecordClassId,
    string[] SlotPath);

internal static class EmbeddedComponentSlotCatalog
{
    private static readonly EmbeddedComponentSlotDefinition[] Slots =
    [
        new(
            "component.avatar.label.editor",
            "label",
            "Label",
            "component.label",
            ["avatar", "labelSlot"]),
        new(
            "component.buttonIcon.label.editor",
            "label",
            "Label",
            "component.label",
            ["buttonIcon", "labelSlot"]),
    ];

    public static bool TryGet(string fieldId, out EmbeddedComponentSlotDefinition slot)
    {
        foreach (var candidate in Slots)
        {
            if (!candidate.FieldId.Equals(fieldId, StringComparison.Ordinal))
            {
                continue;
            }

            slot = candidate;
            return true;
        }

        slot = new EmbeddedComponentSlotDefinition("", "", "", "", []);
        return false;
    }

    public static IReadOnlyList<EmbeddedComponentSlotDefinition> All()
    {
        return Slots;
    }

    public static EmbeddedComponentSlotDefinition Get(string fieldId)
    {
        return TryGet(fieldId, out var slot)
            ? slot
            : throw new InvalidOperationException($"Unknown embedded component slot '{fieldId}'.");
    }
}
