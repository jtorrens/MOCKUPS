import {
  defineFields,
  getJsonValueAtPath,
  type JsonFieldBinding,
} from "../../value-system/index.js";

export const CHAT_TEXT_INPUT_BAR_FIELDS = defineFields({
  cursorWidth: {
    id: "chat.textInputBar.cursorWidth",
    kind: "decimal",
    defaultValue: 2,
    ui: {
      label: "Cursor width",
      min: 0,
      step: 1,
    },
  },
  cursorBlinkFrames: {
    id: "chat.textInputBar.cursorBlinkFrames",
    kind: "integer",
    defaultValue: 15,
    ui: {
      label: "Cursor blink",
      min: 0,
      step: 1,
    },
  },
  cursorColor: {
    id: "chat.textInputBar.cursorColor",
    kind: "themeColorToken",
    defaultValue: "icons.accent",
    ui: {
      label: "Cursor color",
      semanticTokenGroup: "icons",
      options: ["icons.accent", "icons.primary", "icons.secondary"],
    },
  },
  idleTextColor: {
    id: "chat.textInputBar.idleTextColor",
    kind: "themeColorToken",
    defaultValue: "icons.secondary",
    ui: {
      label: "Idle text color",
      semanticTokenGroup: "icons",
      options: ["icons.secondary", "icons.primary", "colors.text"],
    },
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

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export function unscaleTextInputBarThemeScope(
  themeTokens: Record<string, unknown>,
  scale: number,
): Record<string, unknown> {
  const cursor = isObject(themeTokens.cursor) ? themeTokens.cursor : {};
  const cursorWidth = getJsonValueAtPath(cursor, ["width"]);
  if (typeof cursorWidth !== "number" || !Number.isFinite(cursorWidth)) {
    return themeTokens;
  }
  return {
    ...themeTokens,
    cursor: {
      ...cursor,
      width: cursorWidth / Math.max(scale, 0.0001),
    },
  };
}
