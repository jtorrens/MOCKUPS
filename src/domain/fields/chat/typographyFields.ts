import type { JsonFieldBinding } from "../../value-system/index.js";

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

export const CHAT_TYPOGRAPHY_GROUPS = [
  "message",
  "headerTitle",
  "headerSubtitle",
] as const;

const CHAT_TYPOGRAPHY_PROPERTY_FIELDS = {
  fontFamily: {
    kind: "fontFamily",
    ui: {
      label: "Font family",
      lockFontFamily: true,
    },
  },
  fontSize: {
    kind: "decimal",
    ui: {
      label: "Font size",
      min: 1,
      step: 1,
    },
  },
  lineHeight: {
    kind: "decimal",
    ui: {
      label: "Line height",
      min: 1,
      step: 1,
    },
  },
  fontWeight: {
    kind: "fontWeight",
    ui: {
      label: "Font weight",
    },
  },
  fontStyle: {
    kind: "fontStyle",
    ui: {
      label: "Font style",
    },
  },
} as const;

const CHAT_TYPOGRAPHY_GROUP_LABELS: Record<
  (typeof CHAT_TYPOGRAPHY_GROUPS)[number],
  string
> = {
  message: "Message",
  headerTitle: "Header title",
  headerSubtitle: "Header subtitle",
};

export const CHAT_TYPOGRAPHY_TOKEN_BINDINGS =
  CHAT_TYPOGRAPHY_GROUPS.flatMap((group) =>
    Object.entries(CHAT_TYPOGRAPHY_PROPERTY_FIELDS).map(
      ([property, definition]) => ({
        outputPath: ["typography", group, property],
        field: {
          id: `chat.typography.${group}.${property}`,
          ...definition,
          ui: {
            ...definition.ui,
            label: `${CHAT_TYPOGRAPHY_GROUP_LABELS[group]} ${
              definition.ui.label
            }`,
          },
        },
      }),
    ),
  ) satisfies readonly JsonFieldBinding[];

export function stripChatModuleTypographyFontIdentity(
  tokens: Record<string, unknown>,
): Record<string, unknown> {
  const typography = isObject(tokens.typography) ? tokens.typography : undefined;
  if (!typography) return tokens;

  const nextTypography: Record<string, unknown> = { ...typography };
  for (const group of CHAT_TYPOGRAPHY_GROUPS) {
    const entry = nextTypography[group];
    if (!isObject(entry)) continue;
    const nextEntry = { ...entry };
    delete nextEntry.fontFamily;
    delete nextEntry.family;
    delete nextEntry.productionFontId;
    delete nextEntry.source;
    nextTypography[group] = nextEntry;
  }

  return {
    ...tokens,
    typography: nextTypography,
  };
}
