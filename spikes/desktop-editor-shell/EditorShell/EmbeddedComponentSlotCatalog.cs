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
            "component.textInput.barSurface.editor",
            "surface",
            "Bar surface",
            "component.surface",
            ["textInput", "barSurfaceSlot"]),
        new(
            "component.textInput.textBox.editor",
            "textBox",
            "Text box",
            "component.textBox",
            ["textInput", "textBoxSlot"]),
        new(
            "component.textInput.idleLeftIconRow.editor",
            "iconRow",
            "Idle left icons",
            "component.iconRow",
            ["textInput", "idleLeftIconRowSlot"]),
        new(
            "component.textInput.idleRightIconRow.editor",
            "iconRow",
            "Idle right icons",
            "component.iconRow",
            ["textInput", "idleRightIconRowSlot"]),
        new(
            "component.textInput.typingLeftIconRow.editor",
            "iconRow",
            "Typing left icons",
            "component.iconRow",
            ["textInput", "typingLeftIconRowSlot"]),
        new(
            "component.textInput.typingRightIconRow.editor",
            "iconRow",
            "Typing right icons",
            "component.iconRow",
            ["textInput", "typingRightIconRowSlot"]),
        new(
            "component.buttonIcon.surface.editor",
            "surface",
            "Surface",
            "component.surface",
            ["buttonIcon", "surfaceSlot"]),
        new(
            "component.buttonIcon.label.editor",
            "label",
            "Label",
            "component.label",
            ["buttonIcon", "labelSlot"]),
        new(
            "component.textBox.surface.editor",
            "surface",
            "Surface",
            "component.surface",
            ["textBox", "surfaceSlot"]),
        new(
            "component.textBox.cursor.editor",
            "cursor",
            "Cursor",
            "component.cursor",
            ["textBox", "cursorSlot"]),
        new(
            "component.iconRow.buttonIcon.editor",
            "buttonIcon",
            "Button icon",
            "component.buttonIcon",
            ["iconRow", "buttonIconSlot"]),
        new(
            "component.label.surface.editor",
            "surface",
            "Surface",
            "component.surface",
            ["label", "surfaceSlot"]),
        new(
            "component.audio.surface.editor",
            "surface",
            "Surface",
            "component.surface",
            ["audio", "surfaceSlot"]),
        new(
            "component.audio.avatar.editor",
            "avatar",
            "Avatar",
            "component.avatar",
            ["audio", "avatarSlot"]),
        new(
            "component.audio.badge.editor",
            "buttonIcon",
            "Badge",
            "component.buttonIcon",
            ["audio", "badgeSlot"]),
        new(
            "component.video.surface.editor",
            "surface",
            "Surface",
            "component.surface",
            ["video", "surfaceSlot"]),
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
