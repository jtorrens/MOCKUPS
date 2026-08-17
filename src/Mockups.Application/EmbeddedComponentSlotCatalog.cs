using System;
using System.Collections.Generic;

namespace Mockups.DesktopEditorShell.EditorShell;

public sealed record EmbeddedComponentSlotDefinition(
    string FieldId,
    string EmbeddedComponentType,
    string Label,
    string RecordClassId,
    string[] SlotPath);

public static class EmbeddedComponentSlotCatalog
{
    private static readonly EmbeddedComponentSlotDefinition[] Slots =
    [
        new(
            "module.core.lockScreen.statusBarVariant",
            "status_bar",
            "Status bar",
            "component.status_bar",
            ["lockScreen", "statusBarSlot"]),
        new(
            "module.core.lockScreen.navigationBarVariant",
            "navigation_bar",
            "Navigation bar",
            "component.navigation_bar",
            ["lockScreen", "navigationBarSlot"]),
        new(
            "module.core.lockScreen.stackVariant",
            "componentStack",
            "Stack",
            "component.componentStack",
            ["lockScreen", "stackSlot"]),
        new("module.core.chat.headerSurface.editor", "surface", "Header surface", "component.surface", ["conversation", "headerSurfaceSlot"]),
        new("module.core.chat.headerLeftIconRow.editor", "iconRow", "Left icon row", "component.iconRow", ["conversation", "headerLeftIconRowSlot"]),
        new("module.core.chat.headerRightIconRow.editor", "iconRow", "Right icon row", "component.iconRow", ["conversation", "headerRightIconRowSlot"]),
    ];

    public static bool TryGet(string fieldId, out EmbeddedComponentSlotDefinition slot)
    {
        foreach (var candidate in All())
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
        return
        [
            .. Slots,
            .. GeneratedComponentScaffoldEmbeddedSlots.All,
            .. GeneratedModuleScaffoldEmbeddedSlots.All,
        ];
    }

    public static EmbeddedComponentSlotDefinition Get(string fieldId)
    {
        return TryGet(fieldId, out var slot)
            ? slot
            : throw new InvalidOperationException($"Unknown embedded component slot '{fieldId}'.");
    }
}
