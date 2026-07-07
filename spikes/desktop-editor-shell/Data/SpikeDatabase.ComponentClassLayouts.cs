using Mockups.DesktopEditorShell.EditorShell;
using System;

namespace Mockups.DesktopEditorShell.Data;

internal sealed partial class SpikeDatabase
{
    private static string ComponentClassLayoutCardsJson(string recordClassId)
    {
        var typeSpecific = recordClassId switch
        {
            "component.surface" => $$"""
            ,
            {
              "id": "surface",
              "label": "Surface",
              "subtitle": "Reusable visual surface appearance",
              "icon": "{{EditorIcons.Style}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "surface", "label": "Surface", "order": 10, "visible": true, "fields": [
                  { "id": "component.surface.backgroundColorToken", "order": 10, "visible": true },
                  { "id": "component.surface.backgroundAlpha", "order": 20, "visible": true },
                  { "id": "component.surface.borderAlpha", "order": 30, "visible": true }
                ] },
                { "id": "tail", "label": "Tail", "order": 20, "visible": true, "fields": [
                  { "id": "component.surface.tail.enabled", "order": 10, "visible": true },
                  { "id": "component.surface.tail.style", "order": 20, "visible": true },
                  { "id": "component.surface.tail.side", "order": 30, "visible": true },
                  { "id": "component.surface.tail.vertical", "order": 40, "visible": true },
                  { "id": "component.surface.tail.size", "order": 50, "visible": true }
                ] }
              ]
            }
            """,
            "component.cursor" => $$"""
            ,
            {
              "id": "cursor",
              "label": "Cursor",
              "subtitle": "Theme color and fade timing",
              "icon": "{{EditorIcons.TextInput}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "cursor", "label": "Cursor", "order": 10, "visible": true, "fields": [
                  { "id": "component.cursor.colorToken", "order": 10, "visible": true },
                  { "id": "component.cursor.width", "order": 20, "visible": true },
                  { "id": "component.cursor.minimumFade", "order": 30, "visible": true },
                  { "id": "component.cursor.fadeFrames", "order": 40, "visible": true }
                ] }
              ]
            }
            """,
            "component.textBox" => $$"""
            ,
            {
              "id": "textBox",
              "label": "Text Box",
              "subtitle": "Reusable text field content box",
              "icon": "{{EditorIcons.TextInput}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "textBox", "label": "Text box", "order": 10, "visible": true, "fields": [
                  { "id": "component.textBox.dimensionMode", "order": 10, "visible": true },
                  { "id": "component.textBox.padding", "order": 30, "visible": true },
                  { "id": "component.textBox.surface.editor", "order": 40, "visible": true },
                  { "id": "component.textBox.textColorToken", "order": 60, "visible": true },
                  { "id": "component.textBox.placeholderColorToken", "order": 70, "visible": true },
                  { "id": "component.textBox.typography", "order": 80, "visible": true },
                  { "id": "component.textBox.textAlign", "order": 90, "visible": true },
                  { "id": "component.textBox.overflowMode", "order": 100, "visible": true }
                ] },
                { "id": "textBoxCursor", "label": "Cursor", "order": 20, "visible": true, "fields": [
                  { "id": "component.textBox.cursor.showCursor", "order": 10, "visible": true },
                  { "id": "component.textBox.cursor.editor", "order": 20, "visible": true }
                ] }
              ]
            }
            """,
            "component.iconRow" => $$"""
            ,
            {
              "id": "iconRow",
              "label": "Icon Row",
              "subtitle": "Reusable row of button icons",
              "icon": "{{EditorIcons.ButtonIcon}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": []
            }
            """,
            "component.avatar" => $$"""
            ,
            {
              "id": "avatar",
              "label": "Avatar",
              "subtitle": "Reusable avatar presentation defaults",
              "icon": "{{EditorIcons.Avatar}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "avatar", "label": "Avatar", "order": 10, "visible": true, "fields": [
                  { "id": "component.avatar.defaultSize", "order": 10, "visible": true },
                  { "id": "component.avatar.cornerRadiusToken", "order": 20, "visible": true }
                ] },
                { "id": "avatarLabel", "label": "Label", "order": 20, "visible": true, "fields": [
                  { "id": "component.avatar.label.showLabel", "order": 10, "visible": true },
                  { "id": "component.avatar.label.showSubtext", "order": 20, "visible": true },
                  { "id": "component.avatar.label.placement", "order": 30, "visible": true },
                  { "id": "component.avatar.label.editor", "order": 40, "visible": true }
                ] }
              ]
            }
            """,
            "component.status_bar" => $$"""
            ,
            {
              "id": "statusBar",
              "label": "Status Bar",
              "subtitle": "Reusable device status bar composition",
              "icon": "{{EditorIcons.Status}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "layout", "label": "Layout", "order": 10, "visible": true, "fields": [
                  { "id": "component.statusBar.layout.height", "order": 10, "visible": true },
                  { "id": "component.statusBar.layout.itemSize", "order": 20, "visible": true },
                  { "id": "component.statusBar.layout.gap", "order": 30, "visible": true },
                  { "id": "component.statusBar.layout.sidePadding", "order": 40, "visible": true }
                ] }
              ]
            }
            """,
            "component.navigation_bar" => $$"""
            ,
            {
              "id": "navigationBar",
              "label": "Navigation Bar",
              "subtitle": "Reusable device navigation bar composition",
              "icon": "{{EditorIcons.Navigation}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "layout", "label": "Layout", "order": 10, "visible": true, "fields": [
                  { "id": "component.navigationBar.type", "order": 10, "visible": true },
                  { "id": "component.navigationBar.layout.height", "order": 20, "visible": true },
                  { "id": "component.navigationBar.layout.itemSize", "order": 30, "visible": true },
                  { "id": "component.navigationBar.layout.sidePadding", "order": 40, "visible": true },
                  { "id": "component.navigationBar.layout.strokeWidth", "order": 50, "visible": true },
                  { "id": "component.navigationBar.layout.cornerRadius", "order": 60, "visible": true },
                  { "id": "component.navigationBar.layout.filled", "order": 70, "visible": true }
                ] },
                { "id": "gesture", "label": "Gesture", "order": 20, "visible": true, "fields": [
                  { "id": "component.navigationBar.gesture.width", "order": 10, "visible": true },
                  { "id": "component.navigationBar.gesture.height", "order": 20, "visible": true },
                  { "id": "component.navigationBar.gesture.cornerRadius", "order": 30, "visible": true }
                ] }
              ]
            }
            """,
            "component.textInputBar" => $$"""
            ,
            {
              "id": "textInputBar",
              "label": "Bar",
              "subtitle": "Overall input bar frame and surface",
              "icon": "{{EditorIcons.TextInput}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "bar", "label": "Bar", "order": 10, "visible": true, "fields": [
                  { "id": "component.textInput.height", "order": 10, "visible": true },
                  { "id": "component.textInput.barPadding", "order": 20, "visible": true },
                  { "id": "component.textInput.barSurface.editor", "order": 30, "visible": true }
                ] }
              ]
            },
            {
              "id": "textInputBox",
              "label": "Text Box",
              "subtitle": "Text field surface, typography and cursor",
              "icon": "{{EditorIcons.TextInput}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "textBox", "label": "Text box", "order": 10, "visible": true, "fields": [
                  { "id": "component.textInput.textBox.editor", "order": 10, "visible": true },
                  { "id": "component.textInput.textBox.inputs", "order": 20, "visible": true }
                ] }
              ]
            },
            {
              "id": "textInputIcons",
              "label": "Icons",
              "subtitle": "Shared icon spacing and left/right icon rows",
              "icon": "{{EditorIcons.Icon}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "iconDefaults", "label": "Icons", "order": 10, "visible": true, "fields": [
                  { "id": "component.textInput.iconGap", "order": 10, "visible": true },
                  { "id": "component.textInput.iconButton.presetId", "order": 20, "visible": true }
                ] },
                { "id": "idleLeftIcons", "label": "Idle left", "order": 20, "visible": true, "fields": [
                  { "id": "component.textInput.idleLeftIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.textInput.idleLeftIconRow.inputs", "order": 20, "visible": true }
                ] },
                { "id": "idleRightIcons", "label": "Idle right", "order": 30, "visible": true, "fields": [
                  { "id": "component.textInput.idleRightIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.textInput.idleRightIconRow.inputs", "order": 20, "visible": true }
                ] },
                { "id": "typingLeftIcons", "label": "Typing left", "order": 40, "visible": true, "fields": [
                  { "id": "component.textInput.typingLeftIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.textInput.typingLeftIconRow.inputs", "order": 20, "visible": true }
                ] },
                { "id": "typingRightIcons", "label": "Typing right", "order": 50, "visible": true, "fields": [
                  { "id": "component.textInput.typingRightIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.textInput.typingRightIconRow.inputs", "order": 20, "visible": true }
                ] }
              ]
            }
            """,
            "component.keyboard" => $$"""
            ,
            {
              "id": "keyboard",
              "label": "Keyboard",
              "subtitle": "Key shape, pressed behavior and icon slots",
              "icon": "{{EditorIcons.Keyboard}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "keys", "label": "Keys", "order": 10, "visible": true, "fields": [
                  { "id": "component.keyboard.backgroundColorToken", "order": 10, "visible": true },
                  { "id": "component.keyboard.backgroundAlpha", "order": 20, "visible": true },
                  { "id": "component.keyboard.keyBackgroundColorToken", "order": 30, "visible": true },
                  { "id": "component.keyboard.keyTextColorToken", "order": 40, "visible": true },
                  { "id": "component.keyboard.bottomIconColorToken", "order": 50, "visible": true },
                  { "id": "component.keyboard.keyPadding", "order": 60, "visible": true },
                  { "id": "component.keyboard.keyCornerRadius", "order": 70, "visible": true },
                  { "id": "component.keyboard.keyShadowEnabled", "order": 80, "visible": true },
                  { "id": "component.keyboard.pressedEffect", "order": 90, "visible": true },
                  { "id": "component.keyboard.specialKeyTextScale", "order": 100, "visible": true },
                  { "id": "component.keyboard.emojiScale", "order": 110, "visible": true },
                  { "id": "component.keyboard.bottomIconSlots", "order": 120, "visible": true }
                ] }
              ]
            }
            """,
            "component.buttonIcon" => $$"""
            ,
            {
              "id": "buttonIcon",
              "label": "Button Icon",
              "subtitle": "Icon padding and optional label",
              "icon": "{{EditorIcons.ButtonIcon}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "buttonIcon", "label": "Button icon", "order": 10, "visible": true, "fields": [
                  { "id": "component.buttonIcon.size", "order": 10, "visible": true },
                  { "id": "component.buttonIcon.iconPadding", "order": 20, "visible": true },
                  { "id": "component.buttonIcon.surface.editor", "order": 30, "visible": true },
                  { "id": "component.buttonIcon.iconColorToken", "order": 50, "visible": true }
                ] },
                { "id": "buttonIconLabel", "label": "Label", "order": 20, "visible": true, "fields": [
                  { "id": "component.buttonIcon.label.showLabel", "order": 10, "visible": true },
                  { "id": "component.buttonIcon.label.showSubtext", "order": 20, "visible": true },
                  { "id": "component.buttonIcon.label.placement", "order": 30, "visible": true },
                  { "id": "component.buttonIcon.label.editor", "order": 40, "visible": true }
                ] }
              ]
            }
            """,
            "component.label" => $$"""
            ,
            {
              "id": "label",
              "label": "Label",
              "subtitle": "Centered text label dimensions and colors",
              "icon": "{{EditorIcons.Label}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "label", "label": "Label", "order": 10, "visible": true, "fields": [
                  { "id": "component.label.dimensionMode", "order": 10, "visible": true },
                  { "id": "component.label.size", "order": 20, "visible": true },
                  { "id": "component.label.padding", "order": 30, "visible": true },
                  { "id": "component.label.surface.editor", "order": 40, "visible": true },
                  { "id": "component.label.textColorToken", "order": 90, "visible": true },
                  { "id": "component.label.textTypography", "order": 100, "visible": true },
                  { "id": "component.label.textAlign", "order": 120, "visible": true }
                ] },
                { "id": "labelSubtext", "label": "Subtext", "order": 20, "visible": true, "fields": [
                  { "id": "component.label.textGap", "order": 10, "visible": true },
                  { "id": "component.label.subtextColorToken", "order": 20, "visible": true },
                  { "id": "component.label.subtextTypography", "order": 30, "visible": true }
                ] },
                { "id": "labelTransparency", "label": "Transparency", "order": 30, "visible": false, "fields": [] }
              ]
            }
            """,
            "component.audio" => $$"""
            ,
            {
              "id": "audio",
              "label": "Audio",
              "subtitle": "Audio bubble layout and waveform defaults",
              "icon": "{{EditorIcons.Audio}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "audio", "label": "Audio", "order": 10, "visible": true, "fields": [
                  { "id": "component.audio.padding", "order": 10, "visible": true },
                  { "id": "component.audio.surface.editor", "order": 20, "visible": true },
                  { "id": "component.audio.textSize", "order": 40, "visible": true },
                  { "id": "component.audio.textColorToken", "order": 50, "visible": true }
                ] },
                { "id": "audioPlayback", "label": "Playback", "order": 20, "visible": true, "fields": [
                  { "id": "component.audio.playCircleSize", "order": 10, "visible": true },
                  { "id": "component.audio.playIconPadding", "order": 20, "visible": true },
                  { "id": "component.audio.playColorToken", "order": 30, "visible": true },
                  { "id": "component.audio.playIconColorToken", "order": 40, "visible": true },
                  { "id": "component.audio.progressKnobSize", "order": 50, "visible": true }
                ] },
                { "id": "audioWaveform", "label": "Waveform", "order": 30, "visible": true, "fields": [
                  { "id": "component.audio.waveformBarCount", "order": 10, "visible": true },
                  { "id": "component.audio.waveformBarWidth", "order": 20, "visible": true },
                  { "id": "component.audio.waveformGap", "order": 30, "visible": true },
                  { "id": "component.audio.waveformMinHeight", "order": 40, "visible": true },
                  { "id": "component.audio.waveformMaxHeight", "order": 50, "visible": true },
                  { "id": "component.audio.waveformColorToken", "order": 60, "visible": true },
                  { "id": "component.audio.waveformPlayedColorToken", "order": 70, "visible": true }
                ] },
                { "id": "audioAvatar", "label": "Avatar", "order": 40, "visible": true, "fields": [
                  { "id": "component.audio.avatar.showAvatar", "order": 10, "visible": true },
                  { "id": "component.audio.avatar.placement", "order": 20, "visible": true },
                  { "id": "component.audio.avatar.editor", "order": 30, "visible": true }
                ] },
                { "id": "audioBadge", "label": "Badge", "order": 50, "visible": true, "fields": [
                  { "id": "component.audio.badge.showBadge", "order": 10, "visible": true },
                  { "id": "component.audio.badge.iconToken", "order": 20, "visible": true },
                  { "id": "component.audio.badge.backgroundColor", "order": 30, "visible": true },
                  { "id": "component.audio.badge.iconColor", "order": 40, "visible": true },
                  { "id": "component.audio.badge.placement", "order": 50, "visible": true },
                  { "id": "component.audio.badge.editor", "order": 60, "visible": true }
                ] }
              ]
            }
            """,
            "component.video" => $$"""
            ,
            {
              "id": "video",
              "label": "Video",
              "subtitle": "Video status bar and play overlay defaults",
              "icon": "{{EditorIcons.Video}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "video", "label": "Video", "order": 10, "visible": true, "fields": [
                  { "id": "component.video.surface.editor", "order": 10, "visible": true },
                  { "id": "component.video.statusVisible", "order": 30, "visible": true },
                  { "id": "component.video.statusHeight", "order": 40, "visible": true },
                  { "id": "component.video.statusIconSlots", "order": 50, "visible": true },
                  { "id": "component.video.statusTextColorToken", "order": 60, "visible": true },
                  { "id": "component.video.playOverlayVisible", "order": 70, "visible": true },
                  { "id": "component.video.playColorToken", "order": 80, "visible": true }
                ] }
              ]
            }
            """,
            _ => "",
        };

        var style = $$"""
        ,
        {
          "id": "style",
          "label": "Style",
          "subtitle": "Shared surface style defaults",
          "icon": "{{EditorIcons.Style}}",
          "order": 90,
          "visible": true,
          "defaultOpen": false,
          "groups": [
            { "id": "style", "label": "Surface style", "order": 10, "visible": true, "fields": [
              { "id": "component.style.shadowEnabled", "order": 10, "visible": true },
              { "id": "component.style.reliefEnabled", "order": 20, "visible": true },
              { "id": "component.style.borderWidth", "order": 30, "visible": true },
              { "id": "component.style.borderColorToken", "order": 40, "visible": true },
              { "id": "component.style.cornerRadiusToken", "order": 50, "visible": true },
              { "id": "component.style.reliefAngle", "order": 60, "visible": true },
              { "id": "component.style.reliefExtent", "order": 70, "visible": true },
              { "id": "component.style.reliefSpread", "order": 80, "visible": true },
              { "id": "component.style.reliefTopIntensity", "order": 90, "visible": true },
              { "id": "component.style.reliefBottomIntensity", "order": 100, "visible": true }
            ] }
          ]
        }
        """;

        var ownsSurfaceStyle = recordClassId is "component.surface" or "component.avatar" or "component.keyboard";
        return ownsSurfaceStyle ? typeSpecific + style : typeSpecific;
    }
}
