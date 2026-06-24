import { isJsonObject, type JsonValue } from "../../components/json-editor/jsonEditorUtils.js";

export function defaultGroupValue(groupKey: string): JsonValue {
  return groupKey === "messages" || groupKey === "participants" ? [] : {};
}

export function isPrimitiveContentValue(value: JsonValue) {
  return (
    value === null ||
    typeof value === "string" ||
    typeof value === "number" ||
    typeof value === "boolean"
  );
}

export function truncateContentSummary(value: string) {
  const normalized = value.replace(/\s+/g, " ").trim();
  return normalized.length > 96 ? `${normalized.slice(0, 93)}…` : normalized;
}

export function contentSummary(value: JsonValue, groupKey?: string): string {
  if (isJsonObject(value)) {
    if (groupKey === "participants") {
      const name = typeof value.displayName === "string" ? value.displayName : "";
      const role = typeof value.role === "string" ? value.role : "";
      const actor = typeof value.actorId === "string" ? value.actorId : "";
      return truncateContentSummary(
        [name, role, actor ? `actor ${actor}` : ""].filter(Boolean).join(" · "),
      );
    }
    if (groupKey === "messages") {
      const text = typeof value.text === "string" ? value.text : "";
      const direction = value.type === "system" ? "sistema" : "mensaje";
      const start = typeof value.startFrame === "number" ? value.startFrame : null;
      const duration =
        typeof value.enterDurationFrames === "number"
          ? value.enterDurationFrames
          : null;
      const timing =
        start !== null && duration !== null ? `${start}–${start + duration}f` : "";
      const mediaSummary: string = value.media
        ? contentSummary(value.media as JsonValue)
        : "";
      return truncateContentSummary(
        [direction, text || mediaSummary, timing].filter(Boolean).join(" · "),
      );
    }
    for (const key of ["displayName", "text", "title", "name", "role", "type", "id"]) {
      const candidate = value[key];
      if (typeof candidate === "string" && candidate.trim()) {
        return truncateContentSummary(candidate);
      }
    }
    return `${Object.keys(value).length} fields`;
  }
  if (Array.isArray(value)) return `${value.length} items`;
  if (value === null) return "Empty";
  return String(value);
}
