import {
  defineFields,
  type JsonFieldBinding,
} from "../../value-system/index.js";

export const CHAT_TEXT_INPUT_BAR_FIELDS = defineFields({
  cursorWidth: {
    id: "chat.textInputBar.cursorWidth",
    kind: "decimal",
    defaultValue: 2,
  },
  cursorBlinkFrames: {
    id: "chat.textInputBar.cursorBlinkFrames",
    kind: "integer",
    defaultValue: 15,
  },
  cursorColor: {
    id: "chat.textInputBar.cursorColor",
    kind: "themeColorToken",
    defaultValue: "icons.accent",
  },
  idleTextColor: {
    id: "chat.textInputBar.idleTextColor",
    kind: "themeColorToken",
    defaultValue: "icons.secondary",
  },
});

export const CHAT_TEXT_INPUT_BAR_TOKEN_BINDINGS = [
  {
    outputPath: ["cursorWidth"],
    inputPaths: [["cursorWidth"], ["cursor", "width"]],
    field: CHAT_TEXT_INPUT_BAR_FIELDS.cursorWidth,
  },
  {
    outputPath: ["cursorBlinkFrames"],
    inputPaths: [["cursorBlinkFrames"], ["cursor", "blinkFrames"]],
    field: CHAT_TEXT_INPUT_BAR_FIELDS.cursorBlinkFrames,
  },
  {
    outputPath: ["cursorColor"],
    inputPaths: [["cursorColor"], ["cursor", "color"]],
    field: CHAT_TEXT_INPUT_BAR_FIELDS.cursorColor,
  },
  {
    outputPath: ["idleTextColor"],
    inputPaths: [["idleTextColor"]],
    field: CHAT_TEXT_INPUT_BAR_FIELDS.idleTextColor,
  },
] satisfies readonly JsonFieldBinding[];
