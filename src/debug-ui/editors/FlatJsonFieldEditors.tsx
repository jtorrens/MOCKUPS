import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { JsonValueEditor } from "../components/json-editor/JsonValueEditor.js";
import {
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { friendlyGroupLabel } from "../components/json-editor/labels.js";
import { buildJsonUiHints } from "../components/json-editor/uiHints.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";

function looksLikeJson(value: string) {
  const trimmed = value.trim();
  return trimmed.startsWith("{") || trimmed.startsWith("[");
}

function parsedJsonValue(raw: string, fallback: JsonValue): JsonValue {
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

function normalizeGroupValue(value: unknown, fallback: JsonValue): JsonValue {
  if (typeof value === "string" && looksLikeJson(value)) {
    return parsedJsonValue(value, fallback);
  }
  return (value as JsonValue | undefined) ?? fallback;
}

interface FlatJsonObjectEditorProps {
  table: AppTableDefinition;
  field: AppFieldDefinition;
  record: AppRecord | undefined;
  root: Record<string, unknown>;
  omitKeys?: string[];
  onRootChange: (nextRoot: JsonValue) => void;
}

export function FlatJsonObjectEditor({
  table,
  field,
  record,
  root,
  omitKeys = [],
  onRootChange,
}: FlatJsonObjectEditorProps) {
  const visibleEntries = Object.entries(root).filter(
    ([key]) => !omitKeys.includes(key),
  );
  if (visibleEntries.length === 0) {
    return <div className="record-editor-empty-message">No fields yet.</div>;
  }
  return (
    <div className="flat-json-fields">
      {visibleEntries.map(([key, value]) => {
        const jsonValue = normalizeGroupValue(value, "");
        return (
          <InspectorFieldRow
            key={key}
            className="record-editor-field flat-json-row"
            label={<span>{friendlyGroupLabel(key)}</span>}
            control={
              <JsonValueEditor
                rootValue={root as JsonValue}
                path={[key]}
                value={jsonValue}
                hints={buildJsonUiHints(table, field, record)}
                onRootChange={onRootChange}
                onChange={(nextValue) =>
                  onRootChange(setAtPath(root as JsonValue, [key], nextValue))
                }
              />
            }
          />
        );
      })}
    </div>
  );
}

function primitiveJsonPaths(
  value: JsonValue,
  path: JsonPath = [],
): { path: JsonPath; value: JsonValue }[] {
  if (Array.isArray(value)) {
    return value.flatMap((entry, index) =>
      primitiveJsonPaths(entry, [...path, index]),
    );
  }
  if (value !== null && typeof value === "object") {
    return Object.entries(value).flatMap(([key, entry]) =>
      primitiveJsonPaths(entry as JsonValue, [...path, key]),
    );
  }
  return [{ path, value }];
}

function metricLabel(path: JsonPath) {
  return path
    .map((segment) =>
      typeof segment === "number"
        ? String(segment)
        : friendlyGroupLabel(String(segment)),
    )
    .join(" · ");
}

interface DeviceMetricsFieldProps {
  table: AppTableDefinition;
  field: AppFieldDefinition;
  record: AppRecord | undefined;
  root: JsonValue;
  onRootChange: (nextRoot: JsonValue) => void;
}

export function DeviceMetricsField({
  table,
  field,
  record,
  root,
  onRootChange,
}: DeviceMetricsFieldProps) {
  const entries = primitiveJsonPaths(root).filter((entry) => entry.path.length > 0);
  if (entries.length === 0) return null;
  return (
    <div key={field.column} className="flat-json-field-group">
      <div className="flat-json-fields">
        {entries.map(({ path, value }) => (
          <InspectorFieldRow
            key={path.join(".")}
            className="record-editor-field flat-json-row"
            label={<span>{metricLabel(path)}</span>}
            control={
              <JsonValueEditor
                rootValue={root}
                path={path}
                value={value}
                hints={buildJsonUiHints(table, field, record)}
                onRootChange={onRootChange}
                onChange={(nextValue) =>
                  onRootChange(setAtPath(root, path, nextValue))
                }
              />
            }
          />
        ))}
      </div>
    </div>
  );
}
