import { useEffect, useMemo, useState } from "react";
import {
  updateAppRecord,
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { JsonTreeEditor } from "./json-editor/JsonTreeEditor.js";
import {
  hasModeColorOverrides,
  ModeColorEditor,
} from "./json-editor/ModeColorEditor.js";
import { isJsonObject, type JsonValue } from "./json-editor/jsonEditorUtils.js";
import { friendlyGroupLabel } from "./json-editor/labels.js";

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
    "general" | "content" | "behavior" | "overrides"
  >("general");
  const [contentTab, setContentTab] = useState("participants");
  const [appTab, setAppTab] = useState<"general" | "tokens" | "colors" | "notes">(
    "general",
  );
  const [appTokenGroup, setAppTokenGroup] = useState("");
  const [themeTab, setThemeTab] = useState<"general" | "tokens" | "colors">(
    "general",
  );
  const [themeTokenGroup, setThemeTokenGroup] = useState("");
  const [moduleThemeTab, setModuleThemeTab] = useState<"design" | "colors" | "settings">(
    "design",
  );
  const [moduleDesignGroup, setModuleDesignGroup] = useState("");

  useEffect(() => {
    const nextDrafts = Object.fromEntries(
      table.fields.map((field) => [field.column, draftValue(record, field)]),
    );
    setDrafts(nextDrafts);
    setStates(
      Object.fromEntries(table.fields.map((field) => [field.column, "saved"])),
    );
    setErrors({});
    setScreenTab("general");
    setContentTab("participants");
    setModuleThemeTab("design");
    setAppTab("general");
    setAppTokenGroup("");
    setThemeTab("general");
    setThemeTokenGroup("");
    setModuleDesignGroup("");
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
      return value && typeof value === "object" && !Array.isArray(value)
        ? (value as Record<string, unknown>)
        : {};
    } catch {
      return {};
    }
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
    return stringifyJson(
      value && typeof value === "object" && !Array.isArray(value) ? value : {},
    );
  }

  function updateJsonGroupValue(
    column: string,
    groupKey: string,
    nextRawText: string,
  ) {
    const root = parsedObject(drafts[column] ?? "{}");
    setDrafts({
      ...drafts,
      [column]: stringifyJson({
        ...root,
        [groupKey]: parsedObject(nextRawText),
      }),
    });
  }

  function defaultGroupValue(groupKey: string) {
    return groupKey === "messages" || groupKey === "participants" ? [] : {};
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
        className={`app-field app-field-${field.kind} state-${state}`}
      >
        {rawOverride?.hideLabel ? null : (
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
    return (
      <button
        type="button"
        className={`${active ? "active" : ""} ${warning ? "has-warning" : ""}`}
        onClick={onClick}
      >
        {children}
      </button>
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
        : appTokenGroups[0] ?? "";

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
        <div className="editor-tabs">
          <TabButton
            active={appTab === "general"}
            onClick={() => setAppTab("general")}
          >
            General
          </TabButton>
          <TabButton
            active={appTab === "tokens"}
            warning={explicitLocalOverridesInherited(appTokenRoot, inheritedAppRoot)}
            onClick={() => setAppTab("tokens")}
          >
            Tokens
          </TabButton>
          <TabButton
            active={appTab === "colors"}
            warning={explicitLocalOverridesInherited(
              appTokenRoot.modes,
              isJsonObject(inheritedAppRoot as JsonValue)
                ? (inheritedAppRoot as Record<string, unknown>).modes
                : undefined,
            )}
            onClick={() => setAppTab("colors")}
          >
            Colors
          </TabButton>
          <TabButton
            active={appTab === "notes"}
            onClick={() => setAppTab("notes")}
          >
            Notes
          </TabButton>
        </div>
        {appTab === "general" ? (
          <div className="field-stack">
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
        {appTab === "tokens" && configField ? (
          <div className="nested-editor-stack">
            {appTokenGroups.length ? (
              <div className="editor-tabs subtle-tabs">
                {appTokenGroups.map((group) => (
                  <TabButton
                    key={group}
                    active={activeAppTokenGroup === group}
                    onClick={() => setAppTokenGroup(group)}
                  >
                    {friendlyGroupLabel(group)}
                  </TabButton>
                ))}
              </div>
            ) : null}
            <div className="field-stack single-column">
              {activeAppTokenGroup
                ? renderField(configField, {
                    hideLabel: true,
                    rawText: stringifyJson(
                      appTokenRoot[activeAppTokenGroup] &&
                        typeof appTokenRoot[activeAppTokenGroup] === "object" &&
                        !Array.isArray(appTokenRoot[activeAppTokenGroup])
                        ? (appTokenRoot[activeAppTokenGroup] as Record<string, unknown>)
                        : {},
                    ),
                    groupContext: activeAppTokenGroup,
                    inheritedValue:
                      inheritedAppRoot &&
                      typeof inheritedAppRoot === "object" &&
                      !Array.isArray(inheritedAppRoot) &&
                      inheritedAppRoot[activeAppTokenGroup] &&
                      typeof inheritedAppRoot[activeAppTokenGroup] === "object" &&
                      !Array.isArray(inheritedAppRoot[activeAppTokenGroup])
                        ? (inheritedAppRoot[
                            activeAppTokenGroup
                          ] as Record<string, unknown>)
                        : null,
                    onRawTextChange: (nextRawText) =>
                      updateAppTokenRoot({
                        ...appTokenRoot,
                        [activeAppTokenGroup]: parsedObject(nextRawText),
                      } as JsonValue),
                  })
                : renderField(configField, {
                    hideLabel: true,
                    rawText: stringifyJson(appTokenRoot),
                    onRawTextChange: (nextRawText) =>
                      updateAppTokenRoot(parsedObject(nextRawText) as JsonValue),
                  })}
            </div>
          </div>
        ) : null}
        {appTab === "colors" ? (
          <ModeColorEditor
            rootValue={appTokenRoot as JsonValue}
            inheritedRoot={inheritedAppRoot as JsonValue | undefined}
            onRootChange={updateAppTokenRoot}
          />
        ) : null}
        {appTab === "notes" && metadataField ? (
          <div className="field-stack single-column">
            {renderField(metadataField, {
              hideLabel: true,
              rawText: drafts.metadata_json ?? "{}",
              onRawTextChange: (nextRawText) =>
                setDrafts({ ...drafts, metadata_json: nextRawText }),
            })}
          </div>
        ) : null}
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
        : themeTokenGroups[0] ?? "";

    return (
      <section className="panel record-editor">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">Theme editor</span>
            <h2>{String(record[table.titleColumn] ?? record.id)}</h2>
          </div>
          <span className="record-id">{record.id}</span>
        </div>
        <div className="editor-tabs">
          <TabButton
            active={themeTab === "general"}
            onClick={() => setThemeTab("general")}
          >
            General
          </TabButton>
          <TabButton
            active={themeTab === "tokens"}
            onClick={() => setThemeTab("tokens")}
          >
            Tokens
          </TabButton>
          <TabButton
            active={themeTab === "colors"}
            onClick={() => setThemeTab("colors")}
          >
            Colors
          </TabButton>
        </div>
        {themeTab === "general" ? (
          <div className="field-stack">
            {renderFields(["id", "production_id", "name", "family", "version"])}
          </div>
        ) : null}
        {themeTab === "tokens" && tokensField ? (
          <div className="nested-editor-stack">
            {themeTokenGroups.length ? (
              <div className="editor-tabs subtle-tabs">
                {themeTokenGroups.map((group) => (
                  <TabButton
                    key={group}
                    active={activeThemeTokenGroup === group}
                    onClick={() => setThemeTokenGroup(group)}
                  >
                    {friendlyGroupLabel(group)}
                  </TabButton>
                ))}
              </div>
            ) : null}
            <div className="field-stack single-column">
              {activeThemeTokenGroup
                ? renderField(tokensField, {
                    hideLabel: true,
                    rawText: rawForJsonGroupValue(
                      "tokens_json",
                      activeThemeTokenGroup,
                    ),
                    groupContext: activeThemeTokenGroup,
                    onRawTextChange: (nextRawText) =>
                      updateJsonGroupValue(
                        "tokens_json",
                        activeThemeTokenGroup,
                        nextRawText,
                      ),
                  })
                : renderField(tokensField)}
            </div>
          </div>
        ) : null}
        {themeTab === "colors" && tokensField ? (
          <ModeColorEditor
            rootValue={themeTokenRoot as JsonValue}
            onRootChange={(nextValue) => setJsonDraft("tokens_json", nextValue)}
          />
        ) : null}
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
        <div className="editor-tabs">
          <TabButton
            active={screenTab === "general"}
            warning={differsFromInherited("transform_json")}
            onClick={() => setScreenTab("general")}
          >
            Generales
          </TabButton>
          <TabButton
            active={screenTab === "content"}
            warning={differsFromInherited("module_data_json")}
            onClick={() => setScreenTab("content")}
          >
            Content
          </TabButton>
          <TabButton
            active={screenTab === "behavior"}
            warning={differsFromInherited("module_config_json")}
            onClick={() => setScreenTab("behavior")}
          >
            Behavior
          </TabButton>
          <TabButton
            active={screenTab === "overrides"}
            warning={hasObjectContent(drafts.module_tokens_override_json)}
            onClick={() => setScreenTab("overrides")}
          >
            Overrides
          </TabButton>
        </div>
        {screenTab === "general" ? (
          <div className="field-stack">
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
        {screenTab === "content" && moduleDataField ? (
          <div className="nested-editor-stack">
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
              {renderField(moduleDataField, {
                rawText: rawForJsonGroup("module_data_json", activeContentTab),
                hideLabel: true,
                onRawTextChange: (nextRawText) =>
                  updateJsonGroup(
                    "module_data_json",
                    activeContentTab,
                    nextRawText,
                  ),
              })}
            </div>
          </div>
        ) : null}
        {screenTab === "behavior" && moduleConfigField ? (
          <div className="field-stack single-column">
            {renderField(moduleConfigField)}
          </div>
        ) : null}
        {screenTab === "overrides" && overrideField ? (
          <div className="field-stack single-column">
            {renderField(overrideField)}
          </div>
        ) : null}
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
        : designGroups[0] ?? "";
    return (
      <section className="panel record-editor">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">Screen module editor</span>
            <h2>{String(record[table.titleColumn] ?? record.id)}</h2>
          </div>
          <span className="record-id">{record.id}</span>
        </div>
        <div className="editor-tabs">
          <TabButton
            active={moduleThemeTab === "design"}
            warning={differsFromInherited("tokens_json")}
            onClick={() => setModuleThemeTab("design")}
          >
            Design
          </TabButton>
          <TabButton
            active={moduleThemeTab === "colors"}
            warning={hasModeColorOverrides(
              tokenRoot as JsonValue,
              inheritedFields.tokens_json as JsonValue | undefined,
            )}
            onClick={() => setModuleThemeTab("colors")}
          >
            Colors
          </TabButton>
          <TabButton
            active={moduleThemeTab === "settings"}
            onClick={() => setModuleThemeTab("settings")}
          >
            Settings
          </TabButton>
        </div>
        {moduleThemeTab === "design" && tokensField ? (
          <div className="nested-editor-stack">
            {designGroups.length ? (
              <div className="editor-tabs subtle-tabs">
                {designGroups.map((group) => (
                  <TabButton
                    key={group}
                    active={activeDesignGroup === group}
                    warning={explicitLocalDiffers(
                      tokenRoot[group],
                      inheritedFields.tokens_json?.[group],
                    )}
                    onClick={() => setModuleDesignGroup(group)}
                  >
                    {friendlyGroupLabel(group)}
                  </TabButton>
                ))}
              </div>
            ) : null}
            <div className="field-stack single-column">
              {activeDesignGroup
                ? renderField(tokensField, {
                    rawText: rawForJsonGroupValue(
                      "tokens_json",
                      activeDesignGroup,
                    ),
                    hideLabel: true,
                    groupContext: activeDesignGroup,
                    inheritedValue:
                      inheritedFields.tokens_json &&
                      typeof inheritedFields.tokens_json === "object"
                        ? (inheritedFields.tokens_json[
                            activeDesignGroup
                          ] as Record<string, unknown>)
                        : undefined,
                    onRawTextChange: (nextRawText) =>
                      updateJsonGroupValue(
                        "tokens_json",
                        activeDesignGroup,
                        nextRawText,
                      ),
                  })
                : renderField(tokensField)}
            </div>
          </div>
        ) : null}
        {moduleThemeTab === "colors" && tokensField ? (
          <ModeColorEditor
            rootValue={tokenRoot as JsonValue}
            inheritedRoot={inheritedFields.tokens_json as JsonValue | undefined}
            onRootChange={(nextValue) => setJsonDraft("tokens_json", nextValue)}
          />
        ) : null}
        {moduleThemeTab === "settings" ? (
          <div className="nested-editor-stack">
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
