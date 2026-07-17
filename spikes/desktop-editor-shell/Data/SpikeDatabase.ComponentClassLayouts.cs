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
              "groupLayout": "separatedSections",
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
                  { "id": "component.surface.tail.size", "order": 50, "visible": true },
                  { "id": "component.surface.tail.outerCornerRadius", "order": 60, "visible": true }
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
                  { "id": "component.cursor.fadeDurationMs", "order": 40, "visible": true }
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
              "groupLayout": "separatedSections",
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
              "groups": [
                { "id": "sizes", "label": "Sizes", "order": 10, "visible": true, "fields": [
                  { "id": "component.iconRow.sizeSource", "order": 10, "visible": true },
                  { "id": "component.iconRow.iconSizeToken", "order": 20, "visible": true },
                  { "id": "component.iconRow.textSizeToken", "order": 30, "visible": true }
                ] }
              ]
            }
            """,
            "component.iconBar" => $$"""
            ,
            {
              "id": "iconBar",
              "label": "Icon Bar",
              "subtitle": "Three aligned icon rows with two states",
              "icon": "{{EditorIcons.Icon}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "iconBar", "label": "Icon bar", "order": 10, "visible": true, "fields": [
                  { "id": "component.iconBar.edgePadding", "order": 10, "visible": true },
                  { "id": "component.iconBar.sizeSource", "order": 20, "visible": true },
                  { "id": "component.iconBar.iconSizeToken", "order": 30, "visible": true },
                  { "id": "component.iconBar.textSizeToken", "order": 40, "visible": true }
                ] }
              ]
            },
            {
              "id": "iconBarIdle",
              "label": "Idle",
              "subtitle": "Icon rows used by the idle state",
              "icon": "{{EditorIcons.Icon}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "verticalCards",
              "groups": [
                { "id": "idleLeft", "label": "Left", "icon": "{{EditorIcons.SemanticAsset("Left icon row")}}", "order": 10, "visible": true, "collapsible": true, "exclusive": true, "defaultOpen": false, "fields": [
                  { "id": "component.iconBar.idleLeftIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.iconBar.idleLeftIconRow.inputs", "order": 20, "visible": true }
                ] },
                { "id": "idleCenter", "label": "Center", "icon": "{{EditorIcons.SemanticAsset("Center")}}", "order": 20, "visible": true, "collapsible": true, "exclusive": true, "defaultOpen": false, "fields": [
                  { "id": "component.iconBar.idleCenterIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.iconBar.idleCenterIconRow.inputs", "order": 20, "visible": true }
                ] },
                { "id": "idleRight", "label": "Right", "icon": "{{EditorIcons.SemanticAsset("Right icon row")}}", "order": 30, "visible": true, "collapsible": true, "exclusive": true, "defaultOpen": false, "fields": [
                  { "id": "component.iconBar.idleRightIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.iconBar.idleRightIconRow.inputs", "order": 20, "visible": true }
                ] }
              ]
            },
            {
              "id": "iconBarActive",
              "label": "Active",
              "subtitle": "Icon rows used by the active state",
              "icon": "{{EditorIcons.Icon}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "verticalCards",
              "groups": [
                { "id": "activeLeft", "label": "Left", "icon": "{{EditorIcons.SemanticAsset("Left icon row")}}", "order": 10, "visible": true, "collapsible": true, "exclusive": true, "defaultOpen": false, "fields": [
                  { "id": "component.iconBar.activeLeftIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.iconBar.activeLeftIconRow.inputs", "order": 20, "visible": true }
                ] },
                { "id": "activeCenter", "label": "Center", "icon": "{{EditorIcons.SemanticAsset("Center")}}", "order": 20, "visible": true, "collapsible": true, "exclusive": true, "defaultOpen": false, "fields": [
                  { "id": "component.iconBar.activeCenterIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.iconBar.activeCenterIconRow.inputs", "order": 20, "visible": true }
                ] },
                { "id": "activeRight", "label": "Right", "icon": "{{EditorIcons.SemanticAsset("Right icon row")}}", "order": 30, "visible": true, "collapsible": true, "exclusive": true, "defaultOpen": false, "fields": [
                  { "id": "component.iconBar.activeRightIconRow.editor", "order": 10, "visible": true },
                  { "id": "component.iconBar.activeRightIconRow.inputs", "order": 20, "visible": true }
                ] }
              ]
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
              "groupLayout": "separatedSections",
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
                ] },
                { "id": "avatarBadge", "label": "Badge", "order": 30, "visible": true, "fields": [
                  { "id": "component.avatar.badge.editor", "order": 10, "visible": true },
                  { "id": "component.avatar.badge.placement", "order": 20, "visible": true }
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
              "groupLayout": "separatedSections",
              "groups": [
                { "id": "appearance", "label": "Appearance", "icon": "{{EditorIcons.Style}}", "order": 10, "visible": true, "fields": [
                  { "id": "component.statusBar.foregroundColorToken", "order": 10, "visible": true },
                  { "id": "component.statusBar.backgroundColorToken", "order": 20, "visible": true },
                  { "id": "component.statusBar.backgroundAlpha", "order": 30, "visible": true }
                ] },
                { "id": "layout", "label": "Layout", "icon": "{{EditorIcons.Layout}}", "order": 20, "visible": true, "fields": [
                  { "id": "component.statusBar.layout.height", "order": 10, "visible": true },
                  { "id": "component.statusBar.layout.itemSize", "order": 20, "visible": true },
                  { "id": "component.statusBar.layout.gap", "order": 30, "visible": true },
                  { "id": "component.statusBar.layout.sidePadding", "order": 40, "visible": true }
                ] }
              ]
            },
            {
              "id": "statusBarItems",
              "label": "Items",
              "subtitle": "Status item values and left/right placement",
              "icon": "{{EditorIcons.Status}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "items", "label": "Items", "order": 10, "visible": true, "fields": [
                  { "id": "component.statusBar.items", "order": 10, "visible": true }
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
              "groupLayout": "separatedSections",
              "groups": [
                { "id": "appearance", "label": "Appearance", "icon": "{{EditorIcons.Style}}", "order": 10, "visible": true, "fields": [
                  { "id": "component.navigationBar.foregroundColorToken", "order": 10, "visible": true },
                  { "id": "component.navigationBar.backgroundColorToken", "order": 20, "visible": true },
                  { "id": "component.navigationBar.backgroundAlpha", "order": 30, "visible": true }
                ] },
                { "id": "layout", "label": "Layout", "icon": "{{EditorIcons.Layout}}", "order": 20, "visible": true, "fields": [
                  { "id": "component.navigationBar.type", "order": 10, "visible": true },
                  { "id": "component.navigationBar.layout.height", "order": 20, "visible": true },
                  { "id": "component.navigationBar.layout.itemSize", "order": 30, "visible": true },
                  { "id": "component.navigationBar.layout.sidePadding", "order": 40, "visible": true },
                  { "id": "component.navigationBar.layout.strokeWidth", "order": 50, "visible": true },
                  { "id": "component.navigationBar.layout.cornerRadius", "order": 60, "visible": true },
                  { "id": "component.navigationBar.layout.filled", "order": 70, "visible": true }
                ] },
                { "id": "gesture", "label": "Gesture", "icon": "{{EditorIcons.Behavior}}", "order": 30, "visible": true, "fields": [
                  { "id": "component.navigationBar.gesture.width", "order": 10, "visible": true },
                  { "id": "component.navigationBar.gesture.height", "order": 20, "visible": true },
                  { "id": "component.navigationBar.gesture.cornerRadius", "order": 30, "visible": true }
                ] }
              ]
            },
            {
              "id": "navigationBarItems",
              "label": "Button Items",
              "subtitle": "Generated button placement inside the bar",
              "icon": "{{EditorIcons.Navigation}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "items", "label": "Button Items", "order": 10, "visible": true, "fields": [
                  { "id": "component.navigationBar.items", "order": 10, "visible": true }
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
              "groupLayout": "separatedSections",
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
              "groupLayout": "separatedSections",
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
              "subtitle": "Shared icon spacing and icon bar variant",
              "icon": "{{EditorIcons.Icon}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "separatedSections",
              "groups": [
                { "id": "iconDefaults", "label": "Icons", "order": 10, "visible": true, "fields": [
                  { "id": "component.textInput.iconGap", "order": 10, "visible": true },
                  { "id": "component.textInput.iconBar.editor", "order": 20, "visible": true }
                ] }
              ]
            }
            """,
            "component.keyboard" => $$"""
            ,
            {
              "id": "keyboard",
              "label": "Keyboard",
              "subtitle": "Key shape, pressed behavior and icon bar",
              "icon": "{{EditorIcons.Keyboard}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "verticalCards",
              "groups": [
                { "id": "keys", "label": "Keys", "icon": "{{EditorIcons.SemanticAsset("Keys")}}", "order": 10, "visible": true, "fields": [
                  { "id": "component.keyboard.language", "order": 10, "visible": true },
                  { "id": "component.keyboard.typography", "order": 20, "visible": true },
                  { "id": "component.keyboard.keyPadding", "order": 30, "visible": true },
                  { "id": "component.keyboard.keyCornerRadiusToken", "order": 40, "visible": true },
                  { "id": "component.keyboard.keyBorderWidth", "order": 50, "visible": true },
                  { "id": "component.keyboard.keyShadowEnabled", "order": 60, "visible": true },
                  { "id": "component.keyboard.pressedEffect", "order": 70, "visible": true },
                  { "id": "component.keyboard.emojiScale", "order": 80, "visible": true }
                ] },
                { "id": "iconRows", "label": "Icon bar", "icon": "{{EditorIcons.SemanticAsset("Icon Bar")}}", "order": 20, "visible": true, "fields": [
                  { "id": "component.keyboard.iconRowPlacement", "order": 10, "visible": true },
                  { "id": "component.keyboard.iconRowsHeight", "order": 20, "visible": true },
                  { "id": "component.keyboard.iconEdgePadding", "order": 30, "visible": true },
                  { "id": "component.keyboard.iconBar.editor", "order": 40, "visible": true }
                ] }
              ]
            }
            """,
            "component.button" => $$"""
            ,
            {
              "id": "button",
              "label": "Button",
              "subtitle": "Reusable icon, text or combined action",
              "icon": "{{EditorIcons.ButtonIcon}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "content", "label": "Content", "icon": "{{EditorIcons.Content}}", "presentation": "separatedSections", "order": 10, "visible": true, "fields": [
                  { "id": "component.button.iconToken", "order": 10, "visible": true },
                  { "id": "component.button.contentGapToken", "order": 30, "visible": true }
                ] },
                { "id": "layout", "label": "Layout", "icon": "{{EditorIcons.Layout}}", "presentation": "separatedSections", "order": 20, "visible": true, "fields": [
                  { "id": "component.button.dimensionMode", "order": 10, "visible": true },
                  { "id": "component.button.size", "order": 20, "visible": true },
                  { "id": "component.button.padding", "order": 30, "visible": true }
                ] },
                { "id": "interaction", "label": "Interaction", "icon": "{{EditorIcons.SemanticAsset("Interaction")}}", "presentation": "verticalCards", "order": 30, "visible": true, "fields": [
                  { "id": "component.button.pushedDurationToken", "order": 10, "visible": true },
                  { "id": "component.button.badge.editor", "order": 20, "visible": true }
                ] },
                { "id": "normalState", "label": "Normal", "icon": "{{EditorIcons.SemanticAsset("Normal")}}", "presentation": "verticalCards", "order": 40, "visible": true, "fields": [
                  { "id": "component.button.states.normal.surface.editor", "order": 10, "visible": true },
                  { "id": "component.button.states.normal.label.editor", "order": 20, "visible": true },
                  { "id": "component.button.states.normal.iconColorToken", "order": 30, "visible": true }
                ] },
                { "id": "activeState", "label": "Active", "icon": "{{EditorIcons.Status}}", "presentation": "verticalCards", "order": 50, "visible": true, "fields": [
                  { "id": "component.button.states.active.surface.editor", "order": 10, "visible": true },
                  { "id": "component.button.states.active.label.editor", "order": 20, "visible": true },
                  { "id": "component.button.states.active.iconColorToken", "order": 30, "visible": true }
                ] },
                { "id": "pushedState", "label": "Pushed", "icon": "{{EditorIcons.SemanticAsset("Push")}}", "presentation": "verticalCards", "order": 60, "visible": true, "fields": [
                  { "id": "component.button.states.pushed.surface.editor", "order": 10, "visible": true },
                  { "id": "component.button.states.pushed.label.editor", "order": 20, "visible": true },
                  { "id": "component.button.states.pushed.iconColorToken", "order": 30, "visible": true }
                ] },
                { "id": "disabledState", "label": "Disabled", "icon": "{{EditorIcons.SemanticAsset("Disabled")}}", "presentation": "verticalCards", "order": 70, "visible": true, "fields": [
                  { "id": "component.button.states.disabled.surface.editor", "order": 10, "visible": true },
                  { "id": "component.button.states.disabled.label.editor", "order": 20, "visible": true },
                  { "id": "component.button.states.disabled.iconColorToken", "order": 30, "visible": true }
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
              "groupLayout": "separatedSections",
              "groups": [
                { "id": "label", "label": "Label", "order": 10, "visible": true, "fields": [
                  { "id": "component.label.dimensionMode", "order": 10, "visible": true },
                  { "id": "component.label.size", "order": 20, "visible": true },
                  { "id": "component.label.padding", "order": 30, "visible": true },
                  { "id": "component.label.surface.editor", "order": 40, "visible": true },
                  { "id": "component.label.textShadowEnabled", "order": 80, "visible": true },
                  { "id": "component.label.textColorToken", "order": 90, "visible": true },
                  { "id": "component.label.textTypography", "order": 100, "visible": true },
                  { "id": "component.label.textAlign", "order": 120, "visible": true }
                ] },
                { "id": "labelSubtext", "label": "Subtext", "order": 20, "visible": true, "fields": [
                  { "id": "component.label.textGapToken", "order": 10, "visible": true },
                  { "id": "component.label.reserveSubtextSpace", "order": 20, "visible": true },
                  { "id": "component.label.subtextVerticalPosition", "order": 30, "visible": true },
                  { "id": "component.label.subtextHorizontalAlign", "order": 40, "visible": true },
                  { "id": "component.label.subtextColorToken", "order": 50, "visible": true },
                  { "id": "component.label.subtextTypography", "order": 60, "visible": true }
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
              "groupLayout": "verticalCards",
              "groups": [
                { "id": "audio", "label": "General", "icon": "{{EditorIcons.General}}", "order": 10, "visible": true, "fields": [
                  { "id": "component.audio.padding", "order": 10, "visible": true },
                  { "id": "component.audio.surface.editor", "order": 20, "visible": true },
                  { "id": "component.audio.durationLabel.editor", "order": 40, "visible": true }
                ] },
                { "id": "audioPlayback", "label": "Playback", "icon": "{{EditorIcons.Play}}", "order": 20, "visible": true, "fields": [
                  { "id": "component.audio.playCircleSize", "order": 10, "visible": true },
                  { "id": "component.audio.playIconPadding", "order": 20, "visible": true },
                  { "id": "component.audio.playColorToken", "order": 30, "visible": true },
                  { "id": "component.audio.playIconColorToken", "order": 40, "visible": true },
                  { "id": "component.audio.progressKnobSize", "order": 50, "visible": true }
                ] },
                { "id": "audioWaveform", "label": "Waveform", "icon": "{{EditorIcons.SemanticAsset("Waveform")}}", "order": 30, "visible": true, "fields": [
                  { "id": "component.audio.waveformBarCount", "order": 10, "visible": true },
                  { "id": "component.audio.waveformGap", "order": 20, "visible": true },
                  { "id": "component.audio.waveformMinHeight", "order": 30, "visible": true },
                  { "id": "component.audio.waveformMaxHeight", "order": 40, "visible": true },
                  { "id": "component.audio.waveformColorToken", "order": 50, "visible": true },
                  { "id": "component.audio.waveformPlayedColorToken", "order": 60, "visible": true }
                ] },
                { "id": "audioAvatar", "label": "Avatar", "icon": "{{EditorIcons.Avatar}}", "order": 40, "visible": true, "fields": [
                  { "id": "component.audio.avatar.showAvatar", "order": 10, "visible": true },
                  { "id": "component.audio.avatar.placement", "order": 20, "visible": true },
                  { "id": "component.audio.avatar.editor", "order": 30, "visible": true }
                ] },
                { "id": "audioBadge", "label": "Badge", "icon": "{{EditorIcons.SemanticAsset("Badge")}}", "order": 50, "visible": true, "fields": [
                  { "id": "component.audio.badge.editor", "order": 10, "visible": true },
                  { "id": "component.audio.badge.placement", "order": 20, "visible": true }
                ] }
              ]
            }
            """,
            "component.media" => $$"""
            ,
            {
              "id": "media",
              "label": "Media",
              "subtitle": "Surface and shared media controls",
              "icon": "{{EditorIcons.Video}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "separatedSections",
              "groups": [
                { "id": "media", "label": "Media", "order": 10, "visible": true, "fields": [
                  { "id": "component.media.surface.editor", "order": 10, "visible": true }
                ] },
                { "id": "mediaControls", "label": "Controls", "order": 20, "visible": true, "fields": [
                  { "id": "component.media.controlBarHeight", "order": 10, "visible": true },
                  { "id": "component.media.iconBarPadding", "order": 20, "visible": true },
                  { "id": "component.media.iconColorTokenOverride", "order": 30, "visible": true },
                  { "id": "component.media.controlsFadeDelayMs", "order": 40, "visible": true },
                  { "id": "component.media.controlsFadeDurationMs", "order": 50, "visible": true }
                ] }
              ]
            },
            {
              "id": "mediaInlineControls",
              "label": "Inline controls",
              "subtitle": "Icon bars shown in inline media state",
              "icon": "{{EditorIcons.Icon}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "mediaInlineControls", "label": "Inline controls", "order": 10, "visible": true, "fields": [
                  { "id": "component.media.inlineTopIconBar.editor", "order": 10, "visible": true },
                  { "id": "component.media.inlineCenterIconBar.editor", "order": 20, "visible": true },
                  { "id": "component.media.inlineBottomIconBar.editor", "order": 30, "visible": true }
                ] }
              ]
            },
            {
              "id": "mediaFullScreenControls",
              "label": "Full screen controls",
              "subtitle": "Icon bars shown in full screen media state",
              "icon": "{{EditorIcons.Icon}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "mediaFullScreenControls", "label": "Full screen controls", "order": 10, "visible": true, "fields": [
                  { "id": "component.media.fullScreenTopIconBar.editor", "order": 10, "visible": true },
                  { "id": "component.media.fullScreenCenterIconBar.editor", "order": 20, "visible": true },
                  { "id": "component.media.fullScreenBottomIconBar.editor", "order": 30, "visible": true }
                ] }
              ]
            },
            {
              "id": "mediaIdleText",
              "label": "Idle text",
              "subtitle": "Text overlay shown while media is idle",
              "icon": "{{EditorIcons.TextInput}}",
              "order": 50,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "mediaIdleText", "label": "Idle text", "order": 10, "visible": true, "fields": [
                  { "id": "component.media.idleText.enabled", "order": 10, "visible": true },
                  { "id": "component.media.idleText.mode", "order": 20, "visible": true },
                  { "id": "component.media.idleText.text", "order": 30, "visible": true },
                  { "id": "component.media.idleText.targetSeconds", "order": 40, "visible": true },
                  { "id": "component.media.idleText.label.editor", "order": 50, "visible": true },
                  { "id": "component.media.idleText.placement", "order": 70, "visible": true }
                ] }
              ]
            },
            {
              "id": "mediaPlayText",
              "label": "Play text",
              "subtitle": "Text overlay shown during playback",
              "icon": "{{EditorIcons.TextInput}}",
              "order": 60,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "mediaPlayText", "label": "Play text", "order": 10, "visible": true, "fields": [
                  { "id": "component.media.playText.enabled", "order": 10, "visible": true },
                  { "id": "component.media.playText.mode", "order": 20, "visible": true },
                  { "id": "component.media.playText.text", "order": 30, "visible": true },
                  { "id": "component.media.playText.targetSeconds", "order": 40, "visible": true },
                  { "id": "component.media.playText.label.editor", "order": 50, "visible": true },
                  { "id": "component.media.playText.placement", "order": 70, "visible": true }
                ] }
              ]
            }
            """,
            "component.bubble" => $$"""
            ,
            {
              "id": "bubble",
              "label": "Bubble",
              "subtitle": "Message surface and padding",
              "icon": "{{EditorIcons.Bubble}}",
              "order": 20,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "bubbleSurface", "label": "Surface", "order": 10, "visible": true, "fields": [
                  { "id": "component.bubble.surface.editor", "order": 10, "visible": true },
                  { "id": "component.bubble.padding", "order": 20, "visible": true }
                ] }
              ]
            },
            {
              "id": "bubbleText",
              "label": "Text",
              "subtitle": "Text box variant used by the message body",
              "icon": "{{EditorIcons.TextInput}}",
              "order": 30,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "bubbleText", "label": "Text", "order": 10, "visible": true, "fields": [
                  { "id": "component.bubble.textBox.editor", "order": 10, "visible": true }
                ] }
              ]
            },
            {
              "id": "bubbleMedia",
              "label": "Media",
              "subtitle": "Optional media content and variants",
              "icon": "{{EditorIcons.Media}}",
              "order": 40,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "bubbleMedia", "label": "Media", "order": 10, "visible": true, "fields": [
                  { "id": "component.bubble.media.type", "order": 10, "visible": true },
                  { "id": "component.bubble.media.position", "order": 20, "visible": true },
                  { "id": "component.bubble.media.image.editor", "order": 30, "visible": true },
                  { "id": "component.bubble.media.video.editor", "order": 40, "visible": true },
                  { "id": "component.bubble.media.audio.editor", "order": 50, "visible": true }
                ] }
              ]
            },
            {
              "id": "bubbleColors",
              "label": "Colors",
              "subtitle": "Incoming, system and outgoing palette colors",
              "icon": "{{EditorIcons.Color}}",
              "order": 50,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "bubbleColors", "label": "Colors", "order": 10, "visible": true, "fields": [
                  { "id": "component.bubble.incomingBackground", "order": 10, "visible": true },
                  { "id": "component.bubble.incomingText", "order": 20, "visible": true },
                  { "id": "component.bubble.systemBackground", "order": 30, "visible": true },
                  { "id": "component.bubble.systemText", "order": 40, "visible": true },
                  { "id": "component.bubble.outgoingBackground", "order": 50, "visible": true },
                  { "id": "component.bubble.outgoingText", "order": 60, "visible": true }
                ] }
              ]
            },
            {
              "id": "bubbleActorLabel",
              "label": "Actor label",
              "subtitle": "Optional embedded actor label",
              "icon": "{{EditorIcons.Label}}",
              "order": 60,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "bubbleActorLabel", "label": "Actor label", "order": 10, "visible": true, "fields": [
                  { "id": "component.bubble.actorLabel.showLabel", "order": 10, "visible": true },
                  { "id": "component.bubble.actorLabel.useActorColor", "order": 20, "visible": true },
                  { "id": "component.bubble.actorLabel.placement", "order": 30, "visible": true },
                  { "id": "component.bubble.actorLabel.editor", "order": 40, "visible": true }
                ] }
              ]
            },
            {
              "id": "bubbleAvatar",
              "label": "Avatar",
              "subtitle": "Optional embedded actor avatar",
              "icon": "{{EditorIcons.Avatar}}",
              "order": 70,
              "visible": true,
              "defaultOpen": false,
              "groups": [
                { "id": "bubbleAvatar", "label": "Avatar", "order": 10, "visible": true, "fields": [
                  { "id": "component.bubble.avatar.showAvatar", "order": 10, "visible": true },
                  { "id": "component.bubble.avatar.placement", "order": 20, "visible": true },
                  { "id": "component.bubble.avatar.editor", "order": 30, "visible": true }
                ] }
              ]
            },
            {
              "id": "bubbleStatus",
              "label": "Status",
              "subtitle": "Message status text and state icons",
              "icon": "{{EditorIcons.Status}}",
              "order": 80,
              "visible": true,
              "defaultOpen": false,
              "groupLayout": "separatedSections",
              "groups": [
                { "id": "bubbleStatusGeneral", "label": "General", "order": 10, "visible": true, "fields": [
                  { "id": "component.bubble.status.size", "order": 10, "visible": true },
                  { "id": "component.bubble.status.textSize", "order": 20, "visible": true },
                  { "id": "component.bubble.status.gap", "order": 30, "visible": true }
                ] },
                { "id": "bubbleStatusSent", "label": "Sent", "order": 20, "visible": true, "fields": [
                  { "id": "component.bubble.status.sent.icon", "order": 10, "visible": true },
                  { "id": "component.bubble.status.sent.color", "order": 20, "visible": true }
                ] },
                { "id": "bubbleStatusDelivered", "label": "Delivered", "order": 30, "visible": true, "fields": [
                  { "id": "component.bubble.status.delivered.icon", "order": 10, "visible": true },
                  { "id": "component.bubble.status.delivered.color", "order": 20, "visible": true }
                ] },
                { "id": "bubbleStatusRead", "label": "Read", "order": 40, "visible": true, "fields": [
                  { "id": "component.bubble.status.read.icon", "order": 10, "visible": true },
                  { "id": "component.bubble.status.read.color", "order": 20, "visible": true }
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

        var behavior = recordClassId is "component.keyboard" or "component.media"
            ? $$"""
        ,
        {
          "id": "behavior",
          "label": "Behavior",
          "subtitle": "Interaction and transition behavior",
          "icon": "{{EditorIcons.Animation}}",
          "order": 95,
          "visible": true,
          "defaultOpen": false,
          "groups": [
            { "id": "motion", "label": "Motion", "order": 10, "visible": true, "fields": [
              { "id": "{{(recordClassId == "component.media" ? "component.media.motion" : "component.keyboard.motion")}}", "order": 10, "visible": true }
            ] }
          ]
        }
        """
            : "";

        var ownsSurfaceStyle = recordClassId is "component.surface" or "component.avatar";
        return ownsSurfaceStyle ? typeSpecific + style + behavior : typeSpecific + behavior;
    }
}
