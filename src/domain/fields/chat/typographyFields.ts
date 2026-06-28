import type { JsonFieldBinding } from "../../value-system/JsonFieldBinding.js";

export const CHAT_TYPOGRAPHY_GROUPS = [
  "message",
  "headerTitle",
  "headerSubtitle",
] as const;

const CHAT_TYPOGRAPHY_PROPERTY_FIELDS = {
  fontFamily: { kind: "fontFamily" },
  fontSize: { kind: "decimal" },
  lineHeight: { kind: "decimal" },
  fontWeight: { kind: "fontWeight" },
  fontStyle: { kind: "fontStyle" },
} as const;

export const CHAT_TYPOGRAPHY_TOKEN_BINDINGS =
  CHAT_TYPOGRAPHY_GROUPS.flatMap((group) =>
    Object.entries(CHAT_TYPOGRAPHY_PROPERTY_FIELDS).map(
      ([property, definition]) => ({
        outputPath: ["typography", group, property],
        field: {
          id: `chat.typography.${group}.${property}`,
          ...definition,
        },
      }),
    ),
  ) satisfies readonly JsonFieldBinding[];
