import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";

export function parsedObject(raw: string): Record<string, unknown> {
  try {
    const value = JSON.parse(raw) as unknown;
    if (typeof value === "string") {
      return parsedObject(value);
    }
    return value && typeof value === "object" && !Array.isArray(value)
      ? (value as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
}

export function looksLikeJson(value: string) {
  const trimmed = value.trim();
  return trimmed.startsWith("{") || trimmed.startsWith("[");
}

export function parsedJsonValue(raw: string, fallback: JsonValue): JsonValue {
  try {
    const value = JSON.parse(raw) as unknown;
    if (typeof value === "string" && looksLikeJson(value)) {
      return parsedJsonValue(value, fallback);
    }
    if (
      value === null ||
      typeof value === "string" ||
      typeof value === "number" ||
      typeof value === "boolean" ||
      Array.isArray(value) ||
      typeof value === "object"
    ) {
      return value as JsonValue;
    }
    return fallback;
  } catch {
    return fallback;
  }
}

export function normalizeGroupValue(
  value: unknown,
  fallback: JsonValue,
): JsonValue {
  if (typeof value === "string" && looksLikeJson(value)) {
    return parsedJsonValue(value, fallback);
  }
  return (value as JsonValue | undefined) ?? fallback;
}
