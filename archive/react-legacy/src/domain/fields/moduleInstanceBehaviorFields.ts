import { defineFields } from "../value-system/index.js";

export const MODULE_INSTANCE_BEHAVIOR_FIELDS = defineFields({
  showHeader: {
    id: "moduleInstance.behavior.showHeader",
    kind: "boolean",
    defaultValue: true,
    ui: { label: "Show header" },
  },
  showStatusBar: {
    id: "moduleInstance.behavior.showStatusBar",
    kind: "boolean",
    defaultValue: true,
    ui: { label: "Show status bar" },
  },
  showNavigationBar: {
    id: "moduleInstance.behavior.showNavigationBar",
    kind: "boolean",
    defaultValue: true,
    ui: { label: "Show navigation bar" },
  },
  showIncomingActorLabels: {
    id: "moduleInstance.behavior.showIncomingActorLabels",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Show incoming actor labels" },
  },
  initialScroll: {
    id: "moduleInstance.behavior.initialScroll",
    kind: "enum",
    defaultValue: "bottom",
    ui: {
      label: "Initial scroll",
      options: ["top", "bottom", "preserve"],
    },
  },
  showTextInputBar: {
    id: "moduleInstance.behavior.showTextInputBar",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Show text input bar" },
  },
  showKeyboard: {
    id: "moduleInstance.behavior.showKeyboard",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Show keyboard" },
  },
  keyboardPressedEffect: {
    id: "moduleInstance.behavior.keyboard.pressedEffect",
    kind: "enum",
    defaultValue: "popover",
    ui: {
      label: "Pressed effect",
      options: ["popover", "inPlace", "none"],
    },
  },
  statusItemEnabled: {
    id: "moduleInstance.behavior.statusBar.item.enabled",
    kind: "boolean",
    defaultValue: true,
    ui: { label: "On" },
  },
  statusItemTextValue: {
    id: "moduleInstance.behavior.statusBar.item.value.text",
    kind: "text",
    defaultValue: "",
    ui: { label: "Value" },
  },
  statusItemBatteryValue: {
    id: "moduleInstance.behavior.statusBar.item.value.battery",
    kind: "integer",
    defaultValue: 0,
    ui: { label: "Value", min: 0, max: 100, step: 1 },
  },
  statusItemSignalValue: {
    id: "moduleInstance.behavior.statusBar.item.value.signal",
    kind: "integer",
    defaultValue: 0,
    ui: { label: "Value", min: 0, max: 4, step: 1 },
  },
  statusItemCharging: {
    id: "moduleInstance.behavior.statusBar.item.charging",
    kind: "boolean",
    defaultValue: false,
    ui: { label: "Charging" },
  },
});
