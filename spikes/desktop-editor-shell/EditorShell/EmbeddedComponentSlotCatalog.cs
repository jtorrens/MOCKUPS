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
            "module.lockScreen.statusBarVariant",
            "status_bar",
            "Status bar",
            "component.status_bar",
            ["lockScreen", "statusBarSlot"]),
        new(
            "module.lockScreen.navigationBarVariant",
            "navigation_bar",
            "Navigation bar",
            "component.navigation_bar",
            ["lockScreen", "navigationBarSlot"]),
        new(
            "module.lockScreen.stackVariant",
            "componentStack",
            "Stack",
            "component.componentStack",
            ["lockScreen", "stackSlot"]),
        new("module.conversation.headerLeftIconRow.editor", "iconRow", "Left icon row", "component.iconRow", ["conversation", "headerLeftIconRowSlot"]),
        new("module.conversation.headerRightIconRow.editor", "iconRow", "Right icon row", "component.iconRow", ["conversation", "headerRightIconRowSlot"]),
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
            "component.textInput.iconBar.editor",
            "iconBar",
            "Icon bar",
            "component.iconBar",
            ["textInput", "iconBarSlot"]),
        new("component.button.states.normal.surface.editor", "surface", "Normal surface", "component.surface", ["button", "states", "normal", "surfaceSlot"]),
        new("component.button.states.normal.label.editor", "label", "Normal label", "component.label", ["button", "states", "normal", "labelSlot"]),
        new("component.button.states.active.surface.editor", "surface", "Active surface", "component.surface", ["button", "states", "active", "surfaceSlot"]),
        new("component.button.states.active.label.editor", "label", "Active label", "component.label", ["button", "states", "active", "labelSlot"]),
        new("component.button.states.pushed.surface.editor", "surface", "Pushed surface", "component.surface", ["button", "states", "pushed", "surfaceSlot"]),
        new("component.button.states.pushed.label.editor", "label", "Pushed label", "component.label", ["button", "states", "pushed", "labelSlot"]),
        new("component.button.states.disabled.surface.editor", "surface", "Disabled surface", "component.surface", ["button", "states", "disabled", "surfaceSlot"]),
        new("component.button.states.disabled.label.editor", "label", "Disabled label", "component.label", ["button", "states", "disabled", "labelSlot"]),
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
            "component.iconBar.idleLeftIconRow.editor",
            "iconRow",
            "Idle left row",
            "component.iconRow",
            ["iconBar", "idleLeftIconRowSlot"]),
        new(
            "component.iconBar.idleCenterIconRow.editor",
            "iconRow",
            "Idle center row",
            "component.iconRow",
            ["iconBar", "idleCenterIconRowSlot"]),
        new(
            "component.iconBar.idleRightIconRow.editor",
            "iconRow",
            "Idle right row",
            "component.iconRow",
            ["iconBar", "idleRightIconRowSlot"]),
        new(
            "component.iconBar.activeLeftIconRow.editor",
            "iconRow",
            "Active left row",
            "component.iconRow",
            ["iconBar", "activeLeftIconRowSlot"]),
        new(
            "component.iconBar.activeCenterIconRow.editor",
            "iconRow",
            "Active center row",
            "component.iconRow",
            ["iconBar", "activeCenterIconRowSlot"]),
        new(
            "component.iconBar.activeRightIconRow.editor",
            "iconRow",
            "Active right row",
            "component.iconRow",
            ["iconBar", "activeRightIconRowSlot"]),
        new(
            "component.keyboard.iconBar.editor",
            "iconBar",
            "Icon bar",
            "component.iconBar",
            ["keyboard", "iconBarSlot"]),
        new("component.keypad.label.editor", "label", "Label", "component.label", ["keypad", "labelSlot"]),
        new("component.codeIndicator.states.initial.empty.editor", "surface", "Initial empty", "component.surface", ["codeIndicator", "states", "initial", "emptySurfaceSlot"]),
        new("component.codeIndicator.states.initial.filled.editor", "surface", "Initial filled", "component.surface", ["codeIndicator", "states", "initial", "filledSurfaceSlot"]),
        new("component.codeIndicator.states.correct.empty.editor", "surface", "Correct empty", "component.surface", ["codeIndicator", "states", "correct", "emptySurfaceSlot"]),
        new("component.codeIndicator.states.correct.filled.editor", "surface", "Correct filled", "component.surface", ["codeIndicator", "states", "correct", "filledSurfaceSlot"]),
        new("component.codeIndicator.states.incorrect.empty.editor", "surface", "Incorrect empty", "component.surface", ["codeIndicator", "states", "incorrect", "emptySurfaceSlot"]),
        new("component.codeIndicator.states.incorrect.filled.editor", "surface", "Incorrect filled", "component.surface", ["codeIndicator", "states", "incorrect", "filledSurfaceSlot"]),
        new("component.password.initialLabel.editor", "label", "Initial label", "component.label", ["password", "initialLabelSlot"]),
        new("component.password.correctLabel.editor", "label", "Correct label", "component.label", ["password", "correctLabelSlot"]),
        new("component.password.incorrectLabel.editor", "label", "Incorrect label", "component.label", ["password", "incorrectLabelSlot"]),
        new("component.password.indicator.editor", "codeIndicator", "Code indicator", "component.codeIndicator", ["password", "indicatorSlot"]),
        new("component.password.keypad.editor", "keypad", "Keypad", "component.keypad", ["password", "keypadSlot"]),
        new("component.password.fingerprint.editor", "fingerprint", "Fingerprint", "component.fingerprint", ["password", "fingerprintSlot"]),
        new("component.password.faceRecognition.editor", "faceRecognition", "Face recognition", "component.faceRecognition", ["password", "faceRecognitionSlot"]),
        new("component.password.drawPassword.editor", "drawPassword", "Draw password", "component.drawPassword", ["password", "drawPasswordSlot"]),
        new("component.password.iconBar.editor", "iconBar", "Icon bar", "component.iconBar", ["password", "iconBarSlot"]),
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
        new("component.audio.durationLabel.editor", "label", "Duration label", "component.label", ["audio", "durationLabelSlot"]),
        new(
            "component.audio.avatar.editor",
            "avatar",
            "Avatar",
            "component.avatar",
            ["audio", "avatarSlot"]),
        new(
            "component.audio.badge.editor",
            "button",
            "Badge",
            "component.button",
            ["audio", "badgeSlot"]),
        new(
            "component.media.surface.editor",
            "surface",
            "Surface",
            "component.surface",
            ["media", "surfaceSlot"]),
        new("component.media.idleText.label.editor", "label", "Idle label", "component.label", ["media", "idleText", "labelSlot"]),
        new("component.media.playText.label.editor", "label", "Play label", "component.label", ["media", "playText", "labelSlot"]),
        new(
            "component.media.inlineTopIconBar.editor",
            "iconBar",
            "Inline top icon bar",
            "component.iconBar",
            ["media", "inlineTopIconBarSlot"]),
        new(
            "component.media.inlineCenterIconBar.editor",
            "iconBar",
            "Inline center icon bar",
            "component.iconBar",
            ["media", "inlineCenterIconBarSlot"]),
        new(
            "component.media.inlineBottomIconBar.editor",
            "iconBar",
            "Inline bottom icon bar",
            "component.iconBar",
            ["media", "inlineBottomIconBarSlot"]),
        new(
            "component.media.fullScreenTopIconBar.editor",
            "iconBar",
            "Full screen top icon bar",
            "component.iconBar",
            ["media", "fullScreenTopIconBarSlot"]),
        new(
            "component.media.fullScreenCenterIconBar.editor",
            "iconBar",
            "Full screen center icon bar",
            "component.iconBar",
            ["media", "fullScreenCenterIconBarSlot"]),
        new(
            "component.media.fullScreenBottomIconBar.editor",
            "iconBar",
            "Full screen bottom icon bar",
            "component.iconBar",
            ["media", "fullScreenBottomIconBarSlot"]),
        new(
            "component.bubble.surface.editor",
            "surface",
            "Surface",
            "component.surface",
            ["bubble", "surfaceSlot"]),
        new(
            "component.bubble.textBox.editor",
            "textBox",
            "Text box",
            "component.textBox",
            ["bubble", "textBoxSlot"]),
        new(
            "component.bubble.media.image.editor",
            "media",
            "Image media",
            "component.media",
            ["bubble", "imageMediaSlot"]),
        new(
            "component.bubble.media.video.editor",
            "media",
            "Video media",
            "component.media",
            ["bubble", "videoMediaSlot"]),
        new(
            "component.bubble.media.audio.editor",
            "audio",
            "Audio media",
            "component.audio",
            ["bubble", "audioSlot"]),
        new(
            "component.bubble.actorLabel.editor",
            "label",
            "Actor label",
            "component.label",
            ["bubble", "actorLabelSlot"]),
        new(
            "component.bubble.avatar.editor",
            "avatar",
            "Avatar",
            "component.avatar",
            ["bubble", "avatarSlot"]),
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
