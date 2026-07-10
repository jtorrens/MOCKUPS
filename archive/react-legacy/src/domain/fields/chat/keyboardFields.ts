import { STANDARD_IOS_KEYBOARD_LAYOUT } from "../../../../../../src/shared/keyboard/standardKeyboardLayout.js";
import {
  defineFields,
  type JsonFieldBinding,
} from "../../value-system/index.js";

export const CHAT_KEYBOARD_FIELDS = defineFields({
  language: {
    id: "chat.keyboard.language",
    kind: "enum",
    defaultValue: STANDARD_IOS_KEYBOARD_LAYOUT.defaultLanguage,
    ui: {
      label: "Language",
      options: ["es", "en"],
    },
  },
  mode: {
    id: "chat.keyboard.mode",
    kind: "enum",
    defaultValue: STANDARD_IOS_KEYBOARD_LAYOUT.defaultMode,
    ui: {
      label: "Mode",
      options: ["lowercase", "uppercase", "numbers", "symbols", "emoji"],
    },
  },
  pushDurationFrames: {
    id: "chat.keyboard.pushDurationFrames",
    kind: "integer",
    defaultValue: 8,
    ui: {
      label: "Push duration",
      min: 0,
      step: 1,
    },
  },
  messageGapToTextInput: {
    id: "chat.keyboard.messageGapToTextInput",
    kind: "decimal",
    defaultValue: 10,
    ui: {
      label: "Message gap to text input",
      min: 0,
      step: 1,
    },
  },
  fontFamily: {
    id: "chat.keyboard.fontFamily",
    kind: "fontFamily",
    defaultValue: "Oswald",
    ui: {
      label: "Font family",
    },
  },
  fontWeight: {
    id: "chat.keyboard.fontWeight",
    kind: "fontWeight",
    defaultValue: 400,
    ui: {
      label: "Font weight",
    },
  },
  fontStyle: {
    id: "chat.keyboard.fontStyle",
    kind: "fontStyle",
    defaultValue: "normal",
    ui: {
      label: "Font style",
    },
  },
  pressedEffect: {
    id: "chat.keyboard.pressedEffect",
    kind: "enum",
    defaultValue: "popover",
    ui: {
      label: "Pressed effect",
      options: ["popover", "inline", "none"],
    },
  },
  keyRadius: {
    id: "chat.keyboard.keyRadius",
    kind: "decimal",
    defaultValue: 7,
    ui: {
      label: "Key radius",
      min: 0,
      step: 1,
    },
  },
  keyPadding: {
    id: "chat.keyboard.keyPadding",
    kind: "decimal",
    defaultValue: 6,
    ui: {
      label: "Key padding",
      min: 0,
      step: 1,
    },
  },
  keyShadowEnabled: {
    id: "chat.keyboard.keyShadowEnabled",
    kind: "boolean",
    defaultValue: true,
    ui: {
      label: "Key shadow",
    },
  },
  surfaceReliefEnabled: {
    id: "chat.keyboard.surfaceReliefEnabled",
    kind: "boolean",
    defaultValue: true,
    ui: {
      label: "Surface relief",
    },
  },
  bottomItems: {
    id: "chat.keyboard.bottomItems",
    kind: "jsonArray",
    ui: {
      label: "Bottom icons",
    },
  },
  extraEmojis: {
    id: "chat.keyboard.extraEmojis",
    kind: "jsonArray",
    ui: {
      label: "Extra emojis",
    },
  },
});

export const CHAT_KEYBOARD_TOKEN_BINDINGS = [
  { outputPath: ["language"], field: CHAT_KEYBOARD_FIELDS.language },
  { outputPath: ["mode"], field: CHAT_KEYBOARD_FIELDS.mode },
  {
    outputPath: ["pushDurationFrames"],
    field: CHAT_KEYBOARD_FIELDS.pushDurationFrames,
  },
  {
    outputPath: ["messageGapToTextInput"],
    field: CHAT_KEYBOARD_FIELDS.messageGapToTextInput,
  },
  { outputPath: ["fontFamily"], field: CHAT_KEYBOARD_FIELDS.fontFamily },
  { outputPath: ["fontWeight"], field: CHAT_KEYBOARD_FIELDS.fontWeight },
  { outputPath: ["fontStyle"], field: CHAT_KEYBOARD_FIELDS.fontStyle },
  { outputPath: ["pressedEffect"], field: CHAT_KEYBOARD_FIELDS.pressedEffect },
  { outputPath: ["keyRadius"], field: CHAT_KEYBOARD_FIELDS.keyRadius },
  { outputPath: ["keyPadding"], field: CHAT_KEYBOARD_FIELDS.keyPadding },
  {
    outputPath: ["keyShadowEnabled"],
    field: CHAT_KEYBOARD_FIELDS.keyShadowEnabled,
  },
  {
    outputPath: ["surfaceReliefEnabled"],
    field: CHAT_KEYBOARD_FIELDS.surfaceReliefEnabled,
  },
  { outputPath: ["bottomItems"], field: CHAT_KEYBOARD_FIELDS.bottomItems },
  { outputPath: ["extraEmojis"], field: CHAT_KEYBOARD_FIELDS.extraEmojis },
] satisfies readonly JsonFieldBinding[];
