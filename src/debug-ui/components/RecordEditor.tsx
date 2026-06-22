import { useEffect, useMemo, useState, type ReactNode } from "react";
import {
  updateAppRecord,
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { JsonTreeEditor } from "./json-editor/JsonTreeEditor.js";
import { JsonValueEditor } from "./json-editor/JsonValueEditor.js";
import {
  hasModeColorOverrides,
  ModeColorEditor,
} from "./json-editor/ModeColorEditor.js";
import {
  cloneJson,
  defaultJsonValue,
  isJsonObject,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "./json-editor/jsonEditorUtils.js";
import { friendlyGroupLabel } from "./json-editor/labels.js";
import { buildJsonUiHints, hintForPath } from "./json-editor/uiHints.js";

type SaveState = "saved" | "dirty" | "invalid" | "saving" | "failed";

interface RecordEditorProps {
  table: AppTableDefinition;
  record: AppRecord | undefined;
  records: Record<string, AppRecord[]>;
  inheritedFields?: Record<string, Record<string, unknown>>;
  onRecordsChanged: (records: AppRecord[]) => void;
  onRecordSaved: (record: AppRecord) => void;
}

function stringifyJson(value: unknown): string {
  return JSON.stringify(value ?? {}, null, 2);
}

function draftValue(record: AppRecord | undefined, field: AppFieldDefinition) {
  if (!record) return "";
  const value = record[field.column];
  return field.kind === "json" ? stringifyJson(value) : String(value ?? "");
}

function parseValue(
  field: AppFieldDefinition,
  raw: string,
): { ok: true; value: unknown } | { ok: false; error: string } {
  if (field.kind === "json") {
    try {
      const parsed: unknown = JSON.parse(raw);
      if (
        parsed === null ||
        Array.isArray(parsed) ||
        typeof parsed !== "object"
      ) {
        return { ok: false, error: "JSON value must be an object." };
      }
      return { ok: true, value: parsed };
    } catch (error) {
      return {
        ok: false,
        error: error instanceof Error ? error.message : String(error),
      };
    }
  }
  if (field.kind === "number") {
    const numberValue = Number(raw);
    if (!Number.isFinite(numberValue)) {
      return { ok: false, error: "Value must be numeric." };
    }
    return { ok: true, value: numberValue };
  }
  return {
    ok: true,
    value: raw === "" && field.nullable ? null : raw,
  };
}

function restoreStrategyForField(
  table: AppTableDefinition,
  field: AppFieldDefinition,
): "remove" | "set" {
  if (
    table.id === "screen_instances" &&
    field.column === "module_tokens_override_json"
  ) {
    return "remove";
  }
  if (
    table.id === "module_theme_configs" &&
    field.column === "tokens_json"
  ) {
    return "set";
  }
  return "remove";
}

function allowArrayStructuralEditsForField(
  table: AppTableDefinition,
  field: AppFieldDefinition,
): boolean {
  return table.id === "screen_instances" && field.column === "module_data_json";
}

function titleForRecord(record: AppRecord, fallbackColumn: string) {
  return String(record[fallbackColumn] ?? record.name ?? record.id);
}

function relationOptionsForField(
  table: AppTableDefinition,
  field: AppFieldDefinition,
  record: AppRecord | undefined,
  records: Record<string, AppRecord[]>,
): { options: { value: string; label: string }[]; allowEmpty: boolean } | undefined {
  if (field.column === "production_id") {
    return {
      allowEmpty: Boolean(field.nullable),
      options: records.productions?.map((item) => ({
        value: item.id,
        label: titleForRecord(item, "name"),
      })) ?? [],
    };
  }
  if (field.column === "episode_id") {
    return {
      allowEmpty: Boolean(field.nullable),
      options: records.episodes?.map((item) => ({
        value: item.id,
        label: titleForRecord(item, "name"),
      })) ?? [],
    };
  }
  if (field.column === "app_id") {
    return {
      allowEmpty: Boolean(field.nullable),
      options: records.apps?.map((item) => ({
        value: item.id,
        label: titleForRecord(item, "name"),
      })) ?? [],
    };
  }
  if (field.column === "theme_id") {
    return {
      allowEmpty: Boolean(field.nullable),
      options: records.themes?.map((item) => ({
        value: item.id,
        label: titleForRecord(item, "name"),
      })) ?? [],
    };
  }
  if (field.column === "owner_actor_id") {
    return {
      allowEmpty: Boolean(field.nullable),
      options: records.actors?.map((item) => ({
        value: item.id,
        label: titleForRecord(item, "display_name"),
      })) ?? [],
    };
  }
  if (field.column === "device_state_id") {
    return {
      allowEmpty: Boolean(field.nullable),
      options: records.device_states?.map((item) => ({
        value: item.id,
        label: titleForRecord(item, "name"),
      })) ?? [],
    };
  }
  if (field.column === "render_preset_id") {
    return {
      allowEmpty: Boolean(field.nullable),
      options: records.render_presets?.map((item) => ({
        value: item.id,
        label: titleForRecord(item, "name"),
      })) ?? [],
    };
  }
  if (table.id === "screen_instances" && field.column === "theme_mode") {
    const shot = records.shots?.find((item) => item.id === record?.shot_id);
    const owner = records.actors?.find(
      (item) => item.id === (shot?.owner_actor_id ?? record?.owner_actor_id),
    );
    const themeId = record?.theme_id ?? owner?.default_theme_id;
    const theme = records.themes?.find((item) => item.id === themeId);
    const tokens = theme?.tokens_json;
    const modes =
      typeof tokens === "object" &&
      tokens !== null &&
      !Array.isArray(tokens) &&
      typeof (tokens as Record<string, unknown>).modes === "object" &&
      (tokens as Record<string, unknown>).modes !== null &&
      !Array.isArray((tokens as Record<string, unknown>).modes)
        ? Object.keys((tokens as Record<string, unknown>).modes as object)
        : ["light", "dark"];
    return {
      allowEmpty: false,
      options: modes.map((mode) => ({ value: mode, label: mode })),
    };
  }
  return undefined;
}

function sectionMeta(label: string) {
  const normalized = label.toLowerCase();
  const meta: Record<string, { icon: string; subtitle: string }> = {
    general: {
      icon: "⌘",
      subtitle: "Identity, timing and structural settings",
    },
    generales: {
      icon: "⌘",
      subtitle: "Identity, timing and structural settings",
    },
    tokens: {
      icon: "◫",
      subtitle: "Semantic tokens inherited by lower levels",
    },
    colors: {
      icon: "◐",
      subtitle: "Mode-aware colors, surfaces and semantic roles",
    },
    notes: {
      icon: "✎",
      subtitle: "Documentation and internal annotations",
    },
    content: {
      icon: "☰",
      subtitle: "Shot-specific screen data and copy",
    },
    "module content": {
      icon: "☰",
      subtitle: "Shot-specific data for this module instance",
    },
    behavior: {
      icon: "⚙",
      subtitle: "Runtime behavior for this screen",
    },
    overrides: {
      icon: "↺",
      subtitle: "Local style overrides against inherited tokens",
    },
    design: {
      icon: "◇",
      subtitle: "Spacing, typography, layout and component tokens",
    },
    settings: {
      icon: "▣",
      subtitle: "Module binding, metadata and technical settings",
    },
    wallpaper: {
      icon: "▧",
      subtitle: "Background and wallpaper behavior",
    },
    fonts: {
      icon: "T",
      subtitle: "Font family, sizes and text weights",
    },
    typography: {
      icon: "T",
      subtitle: "Text styles and hierarchy",
    },
    spacing: {
      icon: "↔",
      subtitle: "Gaps, gutters and layout spacing",
    },
    radii: {
      icon: "◜",
      subtitle: "Corner radius tokens",
    },
    shadows: {
      icon: "◒",
      subtitle: "Shadows and elevation",
    },
    layout: {
      icon: "▦",
      subtitle: "Layout metrics",
    },
    header: {
      icon: "▤",
      subtitle: "Top bar and navigation",
    },
    messages: {
      icon: "☰",
      subtitle: "Message list spacing and behavior",
    },
    chatbubbles: {
      icon: "☵",
      subtitle: "Incoming and outgoing bubble tokens",
    },
    avatars: {
      icon: "◉",
      subtitle: "Avatar sizing and gaps",
    },
    cursor: {
      icon: "⌁",
      subtitle: "Write-on cursor tokens",
    },
    statusbar: {
      icon: "▥",
      subtitle: "Device status bar appearance",
    },
    notifications: {
      icon: "◌",
      subtitle: "Notification card styling",
    },
  };
  return meta[normalized] ?? { icon: "•", subtitle: "" };
}

export function RecordEditor({
  table,
  record,
  records,
  inheritedFields = {},
  onRecordsChanged,
  onRecordSaved,
}: RecordEditorProps) {
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [states, setStates] = useState<Record<string, SaveState>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [screenTab, setScreenTab] = useState<
    "" | "general" | "content" | "behavior" | "overrides"
  >("");
  const [contentTab, setContentTab] = useState("participants");
  const [appTab, setAppTab] = useState<"" | "general" | "tokens" | "colors" | "notes">(
    "",
  );
  const [appTokenGroup, setAppTokenGroup] = useState("");
  const [themeTab, setThemeTab] = useState<"" | "general" | "tokens" | "colors">(
    "",
  );
  const [themeTokenGroup, setThemeTokenGroup] = useState("");
  const [moduleThemeTab, setModuleThemeTab] = useState<"" | "design" | "colors" | "settings">(
    "",
  );
  const [moduleDesignGroup, setModuleDesignGroup] = useState("");
  const [openContentItems, setOpenContentItems] = useState<Record<string, boolean>>(
    {},
  );

  useEffect(() => {
    const nextDrafts = Object.fromEntries(
      table.fields.map((field) => [field.column, draftValue(record, field)]),
    );
    setDrafts(nextDrafts);
    setStates(
      Object.fromEntries(table.fields.map((field) => [field.column, "saved"])),
    );
    setErrors({});
    setScreenTab("");
    setContentTab("participants");
    setModuleThemeTab("");
    setAppTab("");
    setAppTokenGroup("");
    setThemeTab("");
    setThemeTokenGroup("");
    setModuleDesignGroup("");
    setOpenContentItems({});
  }, [record?.id, table.id]);

  const editableFields = useMemo(
    () => table.fields.filter((field) => !field.readonly),
    [table],
  );

  useEffect(() => {
    if (!record) return;
    const timers = editableFields.flatMap((field) => {
      const raw = drafts[field.column];
      if (raw === undefined || raw === draftValue(record, field)) {
        return [];
      }
      const parsed = parseValue(field, raw);
      if (!parsed.ok) {
        setStates((current) => ({
          ...current,
          [field.column]: "invalid",
        }));
        setErrors((current) => ({
          ...current,
          [field.column]: parsed.error,
        }));
        return [];
      }
      const timer = window.setTimeout(() => {
        setStates((current) => ({ ...current, [field.column]: "saving" }));
        setErrors((current) => ({ ...current, [field.column]: "" }));
        void updateAppRecord(table.id, record.id, {
          [field.column]: parsed.value,
        })
          .then((result) => {
            onRecordsChanged(result.records);
            onRecordSaved((result.saved ?? result.record) as AppRecord);
            setStates((current) => ({
              ...current,
              [field.column]: "saved",
            }));
          })
          .catch((error: Error) => {
            setStates((current) => ({
              ...current,
              [field.column]: "failed",
            }));
            setErrors((current) => ({
              ...current,
              [field.column]: error.message,
            }));
          });
      }, 650);
      setStates((current) => ({ ...current, [field.column]: "dirty" }));
      setErrors((current) => ({ ...current, [field.column]: "" }));
      return [timer];
    });
    return () => timers.forEach((timer) => window.clearTimeout(timer));
  }, [drafts, editableFields, onRecordSaved, onRecordsChanged, record, table.id]);

  if (!record) {
    return (
      <section className="panel record-editor empty-record">
        No records in this table yet.
      </section>
    );
  }

  const fieldsByColumn = new Map(table.fields.map((field) => [field.column, field]));

  function parsedObject(raw: string): Record<string, unknown> {
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

  function looksLikeJson(value: string) {
    const trimmed = value.trim();
    return trimmed.startsWith("{") || trimmed.startsWith("[");
  }

  function normalizeGroupValue(value: unknown, fallback: JsonValue): JsonValue {
    if (typeof value === "string" && looksLikeJson(value)) {
      return parsedJsonValue(value, fallback);
    }
    return (value as JsonValue | undefined) ?? fallback;
  }

  function setJsonDraft(column: string, value: JsonValue) {
    setDrafts({
      ...drafts,
      [column]: stringifyJson(value),
    });
  }

  function hasObjectContent(raw: string | undefined) {
    return Object.keys(parsedObject(raw ?? "{}")).length > 0;
  }

  function explicitLocalDiffers(local: unknown, inherited: unknown): boolean {
    if (local && typeof local === "object" && !Array.isArray(local)) {
      return Object.entries(local as Record<string, unknown>).some(
        ([key, value]) =>
          explicitLocalDiffers(
            value,
            inherited && typeof inherited === "object" && !Array.isArray(inherited)
              ? (inherited as Record<string, unknown>)[key]
              : undefined,
          ),
      );
    }
    if (Array.isArray(local)) {
      return JSON.stringify(local) !== JSON.stringify(inherited);
    }
    return JSON.stringify(local) !== JSON.stringify(inherited);
  }

  function explicitLocalOverridesInherited(
    local: unknown,
    inherited: unknown,
  ): boolean {
    if (inherited === undefined) return false;
    if (local && typeof local === "object" && !Array.isArray(local)) {
      if (!inherited || typeof inherited !== "object" || Array.isArray(inherited)) {
        return false;
      }
      return Object.entries(local as Record<string, unknown>).some(
        ([key, value]) =>
          explicitLocalOverridesInherited(
            value,
            (inherited as Record<string, unknown>)[key],
          ),
      );
    }
    if (Array.isArray(local)) {
      return Array.isArray(inherited) &&
        JSON.stringify(local) !== JSON.stringify(inherited);
    }
    return JSON.stringify(local) !== JSON.stringify(inherited);
  }

  function differsFromInherited(column: string) {
    const inherited = inheritedFields[column];
    if (!inherited) return hasObjectContent(drafts[column]);
    return explicitLocalDiffers(parsedObject(drafts[column] ?? "{}"), inherited);
  }

  function rawForJsonGroup(column: string, groupKey: string) {
    const root = parsedObject(drafts[column] ?? "{}");
    return stringifyJson({ [groupKey]: root[groupKey] ?? defaultGroupValue(groupKey) });
  }

  function updateJsonGroup(column: string, groupKey: string, nextRawText: string) {
    const root = parsedObject(drafts[column] ?? "{}");
    const nextGroupRoot = parsedObject(nextRawText);
    setDrafts({
      ...drafts,
      [column]: stringifyJson({
        ...root,
        [groupKey]: nextGroupRoot[groupKey] ?? defaultGroupValue(groupKey),
      }),
    });
  }

  function rawForJsonGroupValue(column: string, groupKey: string) {
    const root = parsedObject(drafts[column] ?? "{}");
    const value = root[groupKey];
    return stringifyJson(normalizeGroupValue(value, defaultGroupValue(groupKey)));
  }

  function updateJsonGroupValue(
    column: string,
    groupKey: string,
    nextRawText: string,
  ) {
    const root = parsedObject(drafts[column] ?? "{}");
    const fallback = defaultGroupValue(groupKey);
    setDrafts({
      ...drafts,
      [column]: stringifyJson({
        ...root,
        [groupKey]: parsedJsonValue(nextRawText, fallback),
      }),
    });
  }

  function defaultGroupValue(groupKey: string) {
    return groupKey === "messages" || groupKey === "participants" ? [] : {};
  }

  function isPrimitiveContentValue(value: JsonValue) {
    return (
      value === null ||
      typeof value === "string" ||
      typeof value === "number" ||
      typeof value === "boolean"
    );
  }

  function truncateContentSummary(value: string) {
    const normalized = value.replace(/\s+/g, " ").trim();
    return normalized.length > 96 ? `${normalized.slice(0, 93)}…` : normalized;
  }

  function contentSummary(value: JsonValue, groupKey?: string): string {
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
        const sender =
          typeof value.senderParticipantId === "string"
            ? value.senderParticipantId
            : "";
        const type = typeof value.type === "string" ? value.type : "";
        const start = typeof value.startFrame === "number" ? value.startFrame : null;
        const duration =
          typeof value.durationFrames === "number" ? value.durationFrames : null;
        const timing =
          start !== null && duration !== null ? `${start}–${start + duration}f` : "";
        const mediaSummary: string = value.media
          ? contentSummary(value.media as JsonValue)
          : "";
        return truncateContentSummary(
          [sender, type, text || mediaSummary, timing]
            .filter(Boolean)
            .join(" · "),
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

  function contentFieldLabel(
    hints: ReturnType<typeof buildJsonUiHints>,
    groupKey: string,
    path: JsonPath,
    fallback: string,
    value: JsonValue,
  ) {
    return hintForPath(hints, path, value, groupKey).label ?? friendlyGroupLabel(fallback);
  }

  function renderContentGroupEditor(
    field: AppFieldDefinition,
    groupKey: string,
  ) {
    const root = parsedObject(drafts.module_data_json ?? "{}");
    const groupValue = normalizeGroupValue(root[groupKey], defaultGroupValue(groupKey));
    const hints = buildJsonUiHints(table, field, record);

    function updateGroupValue(nextValue: JsonValue) {
      setDrafts({
        ...drafts,
        module_data_json: stringifyJson({
          ...root,
          [groupKey]: nextValue,
        }),
      });
    }

    function updateAtPath(path: JsonPath, nextValue: JsonValue) {
      updateGroupValue(setAtPath(groupValue, path, nextValue));
    }

    function renderPrimitiveRow(path: JsonPath, label: string, value: JsonValue) {
      return (
        <label className="content-field-row" key={path.join(".") || label}>
          <span>{contentFieldLabel(hints, groupKey, path, label, value)}</span>
          <JsonValueEditor
            rootValue={groupValue}
            path={path}
            value={value}
            hints={hints}
            groupContext={groupKey}
            onChange={(nextValue) => updateAtPath(path, nextValue)}
            onRootChange={updateGroupValue}
          />
        </label>
      );
    }

    function renderNestedValue(path: JsonPath, label: string, value: JsonValue): ReactNode {
      if (isPrimitiveContentValue(value)) {
        return renderPrimitiveRow(path, label, value);
      }
      if (Array.isArray(value)) {
        return (
          <details className="content-nested-card" key={path.join(".") || label}>
            <summary>
              <span>{contentFieldLabel(hints, groupKey, path, label, value)}</span>
              <small>{value.length} items</small>
            </summary>
            <div className="content-card-fields">
              {value.map((entry, index) =>
                renderNestedValue([...path, index], `[${index}]`, entry),
              )}
            </div>
          </details>
        );
      }
      return (
        <details className="content-nested-card" key={path.join(".") || label}>
          <summary>
            <span>{contentFieldLabel(hints, groupKey, path, label, value)}</span>
            <small>{contentSummary(value, groupKey)}</small>
          </summary>
          <div className="content-card-fields">
            {Object.entries(value).map(([key, entryValue]) =>
              renderNestedValue([...path, key], key, entryValue),
            )}
          </div>
        </details>
      );
    }

    function renderObjectFields(value: Record<string, JsonValue>, basePath: JsonPath) {
      return Object.entries(value).map(([key, entryValue]) =>
        renderNestedValue([...basePath, key], key, entryValue),
      );
    }

    function addArrayItem() {
      const nextItem = defaultJsonValue("object");
      updateGroupValue(Array.isArray(groupValue) ? [...groupValue, nextItem] : [nextItem]);
    }

    function duplicateArrayItem(index: number) {
      if (!Array.isArray(groupValue)) return;
      updateGroupValue([
        ...groupValue.slice(0, index + 1),
        cloneJson(groupValue[index]),
        ...groupValue.slice(index + 1),
      ]);
    }

    function deleteArrayItem(index: number) {
      if (!Array.isArray(groupValue)) return;
      updateGroupValue(groupValue.filter((_, candidateIndex) => candidateIndex !== index));
    }

    function moveArrayItem(index: number, direction: -1 | 1) {
      if (!Array.isArray(groupValue)) return;
      const targetIndex = index + direction;
      if (targetIndex < 0 || targetIndex >= groupValue.length) return;
      const nextValue = [...groupValue];
      const current = nextValue[index];
      nextValue[index] = nextValue[targetIndex];
      nextValue[targetIndex] = current;
      updateGroupValue(nextValue);
    }

    if (Array.isArray(groupValue)) {
      return (
        <div className="content-array-editor">
          {groupValue.map((entryValue, index) => {
            const stableId = isJsonObject(entryValue) && typeof entryValue.id === "string"
              ? entryValue.id
              : String(index);
            const openKey = `${record?.id ?? "record"}:${groupKey}:${stableId}`;
            const isOpen = Boolean(openContentItems[openKey]);
            return (
              <section
                className={`content-item-card ${isOpen ? "open" : ""}`}
                key={stableId}
              >
                <div className="content-item-topbar">
                  <button
                    type="button"
                    className="content-item-header"
                    aria-expanded={isOpen}
                    onClick={() =>
                      setOpenContentItems((current) => ({
                        ...current,
                        [openKey]: !current[openKey],
                      }))
                    }
                  >
                    <span>
                      [{index}] {contentSummary(entryValue, groupKey)}
                    </span>
                  </button>
                  <div className="content-card-actions">
                    <button
                      type="button"
                      disabled={index === 0}
                      onClick={() => moveArrayItem(index, -1)}
                    >
                      ↑
                    </button>
                    <button
                      type="button"
                      disabled={index === groupValue.length - 1}
                      onClick={() => moveArrayItem(index, 1)}
                    >
                      ↓
                    </button>
                    <button type="button" onClick={() => duplicateArrayItem(index)}>
                      ⧉
                    </button>
                    <button type="button" onClick={() => deleteArrayItem(index)}>
                      ⌫
                    </button>
                  </div>
                </div>
                {isOpen ? (
                  <div className="content-card-fields">
                    {isJsonObject(entryValue)
                      ? renderObjectFields(entryValue, [index])
                      : renderNestedValue([index], `[${index}]`, entryValue)}
                  </div>
                ) : null}
              </section>
            );
          })}
          <button type="button" className="content-add-button" onClick={addArrayItem}>
            Add {friendlyGroupLabel(groupKey).replace(/s$/i, "")}
          </button>
        </div>
      );
    }

    if (isJsonObject(groupValue)) {
      return (
        <div className="content-object-editor">
          {renderObjectFields(groupValue, [])}
        </div>
      );
    }

    return (
      <div className="content-object-editor">
        {renderPrimitiveRow([], friendlyGroupLabel(groupKey), groupValue)}
      </div>
    );
  }

  function tokenEditorGroups(
    root: Record<string, unknown>,
    inheritedRoot?: unknown,
  ) {
    const inherited = isJsonObject(inheritedRoot as JsonValue)
      ? (inheritedRoot as Record<string, unknown>)
      : {};
    return Array.from(
      new Set([...Object.keys(root), ...Object.keys(inherited)]),
    ).filter((group) => {
      const value = root[group] ?? inherited[group];
      return (
        group !== "modes" &&
        group !== "colors" &&
        value !== null &&
        typeof value === "object" &&
        !Array.isArray(value)
      );
    });
  }

  function renderField(field: AppFieldDefinition, rawOverride?: {
    rawText: string;
    onRawTextChange: (nextRawText: string) => void;
    inheritedValue?: Record<string, unknown> | null;
    groupContext?: string;
    hideLabel?: boolean;
  }) {
    const state = states[field.column] ?? "saved";
    const error = errors[field.column];
    const relationSelect = relationOptionsForField(
      table,
      field,
      record,
      records,
    );
    return (
      <div
        key={field.column}
        className={`app-field app-field-${field.kind} state-${state} ${
          rawOverride?.hideLabel && field.kind === "json"
            ? "app-field-frameless"
            : ""
        }`}
      >
        {rawOverride?.hideLabel || field.kind === "json" ? null : (
          <span>
            {field.label}
            <em>{field.readonly ? "Read only" : state}</em>
          </span>
        )}
        {field.kind === "json" ? (
          <JsonTreeEditor
            table={table}
            field={field}
            record={record}
            records={records}
            testId={`field-${field.column}`}
            disabled={field.readonly}
            rawText={rawOverride?.rawText ?? drafts[field.column] ?? ""}
            inheritedValue={
              rawOverride && Object.hasOwn(rawOverride, "inheritedValue")
                ? rawOverride.inheritedValue ?? undefined
                : inheritedFields[field.column]
            }
            restoreStrategy={restoreStrategyForField(table, field)}
            groupContext={rawOverride?.groupContext}
            allowArrayStructuralEdits={allowArrayStructuralEditsForField(
              table,
              field,
            )}
            onRawTextChange={
              rawOverride?.onRawTextChange ??
              ((nextRawText) =>
                setDrafts({
                  ...drafts,
                  [field.column]: nextRawText,
                }))
            }
          />
        ) : relationSelect && relationSelect.options.length > 0 ? (
          <select
            data-testid={`field-${field.column}`}
            disabled={field.readonly}
            value={drafts[field.column] ?? ""}
            onChange={(event) =>
              setDrafts({
                ...drafts,
                [field.column]: event.target.value,
              })
            }
          >
            {relationSelect.allowEmpty ? (
              <option value="">Inherited/default</option>
            ) : null}
            {relationSelect.options.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
        ) : (
          <input
            data-testid={`field-${field.column}`}
            disabled={field.readonly}
            type={field.kind === "number" ? "number" : "text"}
            value={drafts[field.column] ?? ""}
            onChange={(event) =>
              setDrafts({
                ...drafts,
                [field.column]: event.target.value,
              })
            }
          />
        )}
        {error ? <strong>{error}</strong> : null}
      </div>
    );
  }

  function renderFields(columns: string[]) {
    return columns
      .map((column) => fieldsByColumn.get(column))
      .filter((field): field is AppFieldDefinition => Boolean(field))
      .map((field) => renderField(field));
  }

  function configGroupHasContent(groupKey: string) {
    const config = parsedObject(drafts.config_json ?? "{}");
    const value = config[groupKey];
    return (
      value !== null &&
      typeof value === "object" &&
      !Array.isArray(value) &&
      Object.keys(value as Record<string, unknown>).length > 0
    );
  }

  function TabButton({
    active,
    warning,
    children,
    onClick,
  }: {
    active: boolean;
    warning?: boolean;
    children: string;
    onClick: () => void;
  }) {
    const meta = sectionMeta(children);
    return (
      <button
        type="button"
        className={`${active ? "active" : ""} ${warning ? "has-warning" : ""}`}
        onClick={onClick}
      >
        <span className="tab-icon" aria-hidden="true">
          {meta.icon}
        </span>
        <span className="tab-copy">
          <span>{children}</span>
          {meta.subtitle ? <small>{meta.subtitle}</small> : null}
        </span>
      </button>
    );
  }

  function SubgroupAccordion({
    group,
    activeGroup,
    warning,
    onToggle,
    children,
  }: {
    group: string;
    activeGroup: string;
    warning?: boolean;
    onToggle: (group: string) => void;
    children: ReactNode;
  }) {
    const active = activeGroup === group;
    return (
      <div className="editor-subsection-card">
        <TabButton
          active={active}
          warning={warning}
          onClick={() => onToggle(active ? "" : group)}
        >
          {friendlyGroupLabel(group)}
        </TabButton>
        {active ? <div className="editor-subsection-body">{children}</div> : null}
      </div>
    );
  }

  if (table.id === "apps") {
    const configField = fieldsByColumn.get("config_json");
    const metadataField = fieldsByColumn.get("metadata_json");
    const appConfigRoot = parsedObject(drafts.config_json ?? "{}");
    const appTokenRoot = isJsonObject(appConfigRoot.tokens_json as JsonValue)
      ? (appConfigRoot.tokens_json as Record<string, unknown>)
      : appConfigRoot;
    const inheritedAppRoot = inheritedFields.config_json;
    const appTokenGroups = tokenEditorGroups(appTokenRoot, inheritedAppRoot);
    const activeAppTokenGroup =
      appTokenGroup && appTokenGroups.includes(appTokenGroup)
        ? appTokenGroup
        : "";

    function updateAppTokenRoot(nextValue: JsonValue) {
      const nextConfig = Object.hasOwn(appConfigRoot, "tokens_json")
        ? { ...appConfigRoot, tokens_json: nextValue }
        : nextValue;
      setJsonDraft("config_json", nextConfig);
    }

    return (
      <section className="panel record-editor">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">App editor</span>
            <h2>{String(record[table.titleColumn] ?? record.id)}</h2>
          </div>
          <span className="record-id">{record.id}</span>
        </div>
        <div className="editor-sections">
          <div className="editor-section-card">
            <TabButton active={appTab === "general"} onClick={() => setAppTab(appTab === "general" ? "" : "general")}>
              General
            </TabButton>
            {appTab === "general" ? (
              <div className="editor-section-body field-stack">
                {renderFields([
                  "id",
                  "production_id",
                  "name",
                  "bundle_key",
                  "app_type",
                  "icon_asset_id",
                ])}
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton
              active={appTab === "tokens"}
              warning={explicitLocalOverridesInherited(appTokenRoot, inheritedAppRoot)}
              onClick={() => setAppTab(appTab === "tokens" ? "" : "tokens")}
            >
              Tokens
            </TabButton>
            {appTab === "tokens" && configField ? (
              <div className="editor-section-body nested-editor-stack">
                {appTokenGroups.map((group) => (
                  <SubgroupAccordion
                    key={group}
                    group={group}
                    activeGroup={activeAppTokenGroup}
                    onToggle={setAppTokenGroup}
                  >
                    <div className="field-stack single-column">
                      {renderField(configField, {
                        hideLabel: true,
                        rawText: stringifyJson(
                          appTokenRoot[group] &&
                            typeof appTokenRoot[group] === "object" &&
                            !Array.isArray(appTokenRoot[group])
                            ? (appTokenRoot[group] as Record<string, unknown>)
                            : {},
                        ),
                        groupContext: group,
                        inheritedValue:
                          inheritedAppRoot &&
                          typeof inheritedAppRoot === "object" &&
                          !Array.isArray(inheritedAppRoot) &&
                          inheritedAppRoot[group] &&
                          typeof inheritedAppRoot[group] === "object" &&
                          !Array.isArray(inheritedAppRoot[group])
                            ? (inheritedAppRoot[group] as Record<string, unknown>)
                            : null,
                        onRawTextChange: (nextRawText) =>
                          updateAppTokenRoot({
                            ...appTokenRoot,
                            [group]: parsedObject(nextRawText),
                          } as JsonValue),
                      })}
                    </div>
                  </SubgroupAccordion>
                ))}
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton
              active={appTab === "colors"}
              warning={explicitLocalOverridesInherited(
                appTokenRoot.modes,
                isJsonObject(inheritedAppRoot as JsonValue)
                  ? (inheritedAppRoot as Record<string, unknown>).modes
                  : undefined,
              )}
              onClick={() => setAppTab(appTab === "colors" ? "" : "colors")}
            >
              Colors
            </TabButton>
            {appTab === "colors" ? (
              <div className="editor-section-body">
                <ModeColorEditor
                  rootValue={appTokenRoot as JsonValue}
                  inheritedRoot={inheritedAppRoot as JsonValue | undefined}
                  onRootChange={updateAppTokenRoot}
                />
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton active={appTab === "notes"} onClick={() => setAppTab(appTab === "notes" ? "" : "notes")}>
              Notes
            </TabButton>
            {appTab === "notes" && metadataField ? (
              <div className="editor-section-body field-stack single-column">
                {renderField(metadataField, {
                  hideLabel: true,
                  rawText: drafts.metadata_json ?? "{}",
                  onRawTextChange: (nextRawText) =>
                    setDrafts({ ...drafts, metadata_json: nextRawText }),
                })}
              </div>
            ) : null}
          </div>
        </div>
      </section>
    );
  }

  if (table.id === "themes") {
    const tokensField = fieldsByColumn.get("tokens_json");
    const themeTokenRoot = parsedObject(drafts.tokens_json ?? "{}");
    const themeTokenGroups = tokenEditorGroups(themeTokenRoot);
    const activeThemeTokenGroup =
      themeTokenGroup && themeTokenGroups.includes(themeTokenGroup)
        ? themeTokenGroup
        : "";

    return (
      <section className="panel record-editor">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">Theme editor</span>
            <h2>{String(record[table.titleColumn] ?? record.id)}</h2>
          </div>
          <span className="record-id">{record.id}</span>
        </div>
        <div className="editor-sections">
          <div className="editor-section-card">
            <TabButton active={themeTab === "general"} onClick={() => setThemeTab(themeTab === "general" ? "" : "general")}>
              General
            </TabButton>
            {themeTab === "general" ? (
              <div className="editor-section-body field-stack">
                {renderFields(["id", "production_id", "name", "family", "version"])}
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton active={themeTab === "tokens"} onClick={() => setThemeTab(themeTab === "tokens" ? "" : "tokens")}>
              Tokens
            </TabButton>
            {themeTab === "tokens" && tokensField ? (
              <div className="editor-section-body nested-editor-stack">
                {themeTokenGroups.map((group) => (
                  <SubgroupAccordion
                    key={group}
                    group={group}
                    activeGroup={activeThemeTokenGroup}
                    onToggle={setThemeTokenGroup}
                  >
                    <div className="field-stack single-column">
                      {renderField(tokensField, {
                        hideLabel: true,
                        rawText: rawForJsonGroupValue("tokens_json", group),
                        groupContext: group,
                        onRawTextChange: (nextRawText) =>
                          updateJsonGroupValue(
                            "tokens_json",
                            group,
                            nextRawText,
                          ),
                      })}
                    </div>
                  </SubgroupAccordion>
                ))}
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton active={themeTab === "colors"} onClick={() => setThemeTab(themeTab === "colors" ? "" : "colors")}>
              Colors
            </TabButton>
            {themeTab === "colors" && tokensField ? (
              <div className="editor-section-body">
                <ModeColorEditor
                  rootValue={themeTokenRoot as JsonValue}
                  onRootChange={(nextValue) => setJsonDraft("tokens_json", nextValue)}
                />
              </div>
            ) : null}
          </div>
        </div>
      </section>
    );
  }

  if (table.id === "screen_instances") {
    const moduleDataField = fieldsByColumn.get("module_data_json");
    const moduleConfigField = fieldsByColumn.get("module_config_json");
    const overrideField = fieldsByColumn.get("module_tokens_override_json");
    const contentGroups = ["participants", "header", "messages"].filter(
      (group) => group in parsedObject(drafts.module_data_json ?? "{}"),
    );
    const safeContentGroups = contentGroups.length
      ? contentGroups
      : ["participants", "header", "messages"];
    const activeContentTab = safeContentGroups.includes(contentTab)
      ? contentTab
      : safeContentGroups[0];

    return (
      <section className="panel record-editor">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">Screen instance editor</span>
            <h2>{String(record[table.titleColumn] ?? record.id)}</h2>
          </div>
          <span className="record-id">{record.id}</span>
        </div>
        <div className="editor-sections">
          <div className="editor-section-card">
            <TabButton
              active={screenTab === "general"}
              warning={differsFromInherited("transform_json")}
              onClick={() => setScreenTab(screenTab === "general" ? "" : "general")}
            >
              Generales
            </TabButton>
            {screenTab === "general" ? (
              <div className="editor-section-body field-stack">
                {renderFields([
                  "id",
                  "shot_id",
                  "app_id",
                  "screen_type",
                  "module_id",
                  "module_schema_version",
                  "device_state_id",
                  "theme_mode",
                  "start_frame",
                  "end_frame",
                  "layer_order",
                  "transform_json",
                ])}
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton
              active={screenTab === "content"}
              warning={differsFromInherited("module_data_json")}
              onClick={() => setScreenTab(screenTab === "content" ? "" : "content")}
            >
              Module Content
            </TabButton>
            {screenTab === "content" && moduleDataField ? (
              <div className="editor-section-body nested-editor-stack">
                <div className="editor-tabs subtle-tabs">
                  {safeContentGroups.map((group) => (
                    <TabButton
                      key={group}
                      active={activeContentTab === group}
                      onClick={() => setContentTab(group)}
                    >
                      {friendlyGroupLabel(group)}
                    </TabButton>
                  ))}
                </div>
                <div className="field-stack single-column">
                  {renderContentGroupEditor(moduleDataField, activeContentTab)}
                </div>
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton
              active={screenTab === "behavior"}
              warning={differsFromInherited("module_config_json")}
              onClick={() => setScreenTab(screenTab === "behavior" ? "" : "behavior")}
            >
              Behavior
            </TabButton>
            {screenTab === "behavior" && moduleConfigField ? (
              <div className="editor-section-body field-stack single-column">
                {renderField(moduleConfigField)}
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton
              active={screenTab === "overrides"}
              warning={hasObjectContent(drafts.module_tokens_override_json)}
              onClick={() => setScreenTab(screenTab === "overrides" ? "" : "overrides")}
            >
              Overrides
            </TabButton>
            {screenTab === "overrides" && overrideField ? (
              <div className="editor-section-body field-stack single-column">
                {renderField(overrideField)}
              </div>
            ) : null}
          </div>
        </div>
      </section>
    );
  }

  if (table.id === "module_theme_configs") {
    const tokensField = fieldsByColumn.get("tokens_json");
    const tokenRoot = parsedObject(drafts.tokens_json ?? "{}");
    const designGroups = Object.keys(tokenRoot).filter(
      (group) => group !== "modes",
    );
    const activeDesignGroup =
      moduleDesignGroup && designGroups.includes(moduleDesignGroup)
        ? moduleDesignGroup
        : "";
    return (
      <section className="panel record-editor">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">Screen module editor</span>
            <h2>{String(record[table.titleColumn] ?? record.id)}</h2>
          </div>
          <span className="record-id">{record.id}</span>
        </div>
        <div className="editor-sections">
          <div className="editor-section-card">
            <TabButton
              active={moduleThemeTab === "design"}
              warning={differsFromInherited("tokens_json")}
              onClick={() => setModuleThemeTab(moduleThemeTab === "design" ? "" : "design")}
            >
              Design
            </TabButton>
            {moduleThemeTab === "design" && tokensField ? (
              <div className="editor-section-body nested-editor-stack">
                {designGroups.map((group) => (
                  <SubgroupAccordion
                    key={group}
                    group={group}
                    activeGroup={activeDesignGroup}
                    warning={explicitLocalDiffers(
                      tokenRoot[group],
                      inheritedFields.tokens_json?.[group],
                    )}
                    onToggle={setModuleDesignGroup}
                  >
                    <div className="field-stack single-column">
                      {renderField(tokensField, {
                        rawText: rawForJsonGroupValue("tokens_json", group),
                        hideLabel: true,
                        groupContext: group,
                        inheritedValue:
                          inheritedFields.tokens_json &&
                          typeof inheritedFields.tokens_json === "object"
                            ? (inheritedFields.tokens_json[
                                group
                              ] as Record<string, unknown>)
                            : undefined,
                        onRawTextChange: (nextRawText) =>
                          updateJsonGroupValue(
                            "tokens_json",
                            group,
                            nextRawText,
                          ),
                      })}
                    </div>
                  </SubgroupAccordion>
                ))}
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton
              active={moduleThemeTab === "colors"}
              warning={hasModeColorOverrides(
                tokenRoot as JsonValue,
                inheritedFields.tokens_json as JsonValue | undefined,
              )}
              onClick={() => setModuleThemeTab(moduleThemeTab === "colors" ? "" : "colors")}
            >
              Colors
            </TabButton>
            {moduleThemeTab === "colors" && tokensField ? (
              <div className="editor-section-body">
                <ModeColorEditor
                  rootValue={tokenRoot as JsonValue}
                  inheritedRoot={inheritedFields.tokens_json as JsonValue | undefined}
                  onRootChange={(nextValue) => setJsonDraft("tokens_json", nextValue)}
                />
              </div>
            ) : null}
          </div>
          <div className="editor-section-card">
            <TabButton
              active={moduleThemeTab === "settings"}
              onClick={() => setModuleThemeTab(moduleThemeTab === "settings" ? "" : "settings")}
            >
              Settings
            </TabButton>
            {moduleThemeTab === "settings" ? (
              <div className="editor-section-body nested-editor-stack">
                <div className="field-stack">
                  {renderFields([
                    "id",
                    "production_id",
                    "theme_id",
                    "app_id",
                    "module_id",
                    "module_schema_version",
                    "name",
                  ])}
                </div>
                {fieldsByColumn.get("metadata_json") ? (
                  <div className="field-stack single-column">
                    {renderField(fieldsByColumn.get("metadata_json")!, {
                      rawText: stringifyJson(
                        Object.fromEntries(
                          Object.entries(parsedObject(drafts.metadata_json ?? "{}")).filter(
                            ([key]) => key !== "default_tokens_json",
                          ),
                        ),
                      ),
                      hideLabel: true,
                      onRawTextChange: (nextRawText) => {
                        const current = parsedObject(drafts.metadata_json ?? "{}");
                        const nextNotes = parsedObject(nextRawText);
                        setDrafts({
                          ...drafts,
                          metadata_json: stringifyJson({
                            ...nextNotes,
                            ...(current.default_tokens_json
                              ? { default_tokens_json: current.default_tokens_json }
                              : {}),
                          }),
                        });
                      },
                    })}
                  </div>
                ) : null}
              </div>
            ) : null}
          </div>
        </div>
      </section>
    );
  }

  return (
    <section className="panel record-editor">
      <div className="panel-heading">
        <div>
          <span className="eyebrow">Record editor</span>
          <h2>{String(record[table.titleColumn] ?? record.id)}</h2>
        </div>
        <span className="record-id">{record.id}</span>
      </div>
      <div className="field-stack">
        {table.fields.map((field) => renderField(field))}
      </div>
    </section>
  );
}
