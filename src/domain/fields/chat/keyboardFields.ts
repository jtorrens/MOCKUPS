import { STANDARD_IOS_KEYBOARD_LAYOUT } from "../../keyboards/standardKeyboardLayout.js";
import type { JsonFieldBinding } from "../../value-system/JsonFieldBinding.js";
import { defineFields } from "../../value-system/FieldDefinition.js";

export const CHAT_KEYBOARD_FIELDS = defineFields({
  language: {
    id: "chat.keyboard.language",
    kind: "enum",
    defaultValue: STANDARD_IOS_KEYBOARD_LAYOUT.defaultLanguage,
  },
  mode: {
    id: "chat.keyboard.mode",
    kind: "enum",
    defaultValue: STANDARD_IOS_KEYBOARD_LAYOUT.defaultMode,
  },
  pushDurationFrames: {
    id: "chat.keyboard.pushDurationFrames",
    kind: "integer",
    defaultValue: 8,
  },
  messageGapToTextInput: {
    id: "chat.keyboard.messageGapToTextInput",
    kind: "decimal",
    defaultValue: 10,
  },
  fontFamily: {
    id: "chat.keyboard.fontFamily",
    kind: "fontFamily",
    defaultValue: "Oswald",
  },
  fontWeight: {
    id: "chat.keyboard.fontWeight",
    kind: "fontWeight",
    defaultValue: 400,
  },
  fontStyle: {
    id: "chat.keyboard.fontStyle",
    kind: "fontStyle",
    defaultValue: "normal",
  },
  pressedEffect: {
    id: "chat.keyboard.pressedEffect",
    kind: "enum",
    defaultValue: "popover",
  },
  keyRadius: {
    id: "chat.keyboard.keyRadius",
    kind: "decimal",
    defaultValue: 7,
  },
  keyPadding: {
    id: "chat.keyboard.keyPadding",
    kind: "decimal",
    defaultValue: 6,
  },
  keyShadowEnabled: {
    id: "chat.keyboard.keyShadowEnabled",
    kind: "boolean",
    defaultValue: true,
  },
  surfaceReliefEnabled: {
    id: "chat.keyboard.surfaceReliefEnabled",
    kind: "boolean",
    defaultValue: true,
  },
  bottomItems: {
    id: "chat.keyboard.bottomItems",
    kind: "jsonArray",
  },
  extraEmojis: {
    id: "chat.keyboard.extraEmojis",
    kind: "jsonArray",
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
