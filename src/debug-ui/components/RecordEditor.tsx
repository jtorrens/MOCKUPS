import { useEffect, useMemo, useState } from "react";
import {
  updateAppRecord,
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { JsonTreeEditor } from "./json-editor/JsonTreeEditor.js";
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
  const [screenTemplateTab, setScreenTemplateTab] = useState<
    "general" | "behavior" | "overrides"
  >("general");
  const [contentTab, setContentTab] = useState("participants");
  const [moduleThemeTab, setModuleThemeTab] = useState<"design" | "theme">(
    "design",
  );
  const [moduleDesignGroup, setModuleDesignGroup] = useState("");
  const [moduleNotesGroup, setModuleNotesGroup] = useState("notes");

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
    setScreenTemplateTab("general");
    setContentTab("participants");
    setModuleThemeTab("design");
    setModuleDesignGroup("");
    setModuleNotesGroup("notes");
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

  function hasObjectContent(raw: string | undefined) {
    return Object.keys(parsedObject(raw ?? "{}")).length > 0;
  }

  function differsFromInherited(column: string) {
    const inherited = inheritedFields[column];
    if (!inherited) return hasObjectContent(drafts[column]);
    return JSON.stringify(parsedObject(drafts[column] ?? "{}")) !==
      JSON.stringify(inherited);
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

  function rawForModuleNotesGroup(groupKey: string) {
    const metadata = parsedObject(drafts.metadata_json ?? "{}");
    const defaultTokens =
      metadata.default_tokens_json &&
      typeof metadata.default_tokens_json === "object" &&
      !Array.isArray(metadata.default_tokens_json)
        ? (metadata.default_tokens_json as Record<string, unknown>)
        : {};
    if (groupKey === "notes") {
      const { default_tokens_json: _defaultTokens, ...notes } = metadata;
      return stringifyJson(notes);
    }
    const value = defaultTokens[groupKey];
    return stringifyJson(
      value && typeof value === "object" && !Array.isArray(value) ? value : {},
    );
  }

  function updateModuleNotesGroup(groupKey: string, nextRawText: string) {
    const metadata = parsedObject(drafts.metadata_json ?? "{}");
    const nextRoot = parsedObject(nextRawText);
    if (groupKey === "notes") {
      setDrafts({
        ...drafts,
        metadata_json: stringifyJson({
          ...nextRoot,
          ...(metadata.default_tokens_json
            ? { default_tokens_json: metadata.default_tokens_json }
            : {}),
        }),
      });
      return;
    }
    const defaultTokens =
      metadata.default_tokens_json &&
      typeof metadata.default_tokens_json === "object" &&
      !Array.isArray(metadata.default_tokens_json)
        ? (metadata.default_tokens_json as Record<string, unknown>)
        : {};
    setDrafts({
      ...drafts,
      metadata_json: stringifyJson({
        ...metadata,
        default_tokens_json: {
          ...defaultTokens,
          [groupKey]: nextRoot,
        },
      }),
    });
  }

  function defaultGroupValue(groupKey: string) {
    return groupKey === "messages" || groupKey === "participants" ? [] : {};
  }

  function renderField(field: AppFieldDefinition, rawOverride?: {
    rawText: string;
    onRawTextChange: (nextRawText: string) => void;
    inheritedValue?: Record<string, unknown>;
    screenTemplateSection?: "behavior" | "overrides";
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
              rawOverride?.inheritedValue ?? inheritedFields[field.column]
            }
            restoreStrategy={restoreStrategyForField(table, field)}
            screenTemplateSection={rawOverride?.screenTemplateSection}
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
              "screen_template_id",
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

  if (table.id === "screen_templates") {
    const configField = fieldsByColumn.get("config_json");

    return (
      <section className="panel record-editor">
        <div className="panel-heading">
          <div>
            <span className="eyebrow">Screen template editor</span>
            <h2>{String(record[table.titleColumn] ?? record.id)}</h2>
          </div>
          <span className="record-id">{record.id}</span>
        </div>
        <div className="editor-tabs">
          <TabButton
            active={screenTemplateTab === "general"}
            warning={hasObjectContent(drafts.default_props_json)}
            onClick={() => setScreenTemplateTab("general")}
          >
            General
          </TabButton>
          <TabButton
            active={screenTemplateTab === "behavior"}
            warning={configGroupHasContent("module_config_json")}
            onClick={() => setScreenTemplateTab("behavior")}
          >
            Behavior
          </TabButton>
          <TabButton
            active={screenTemplateTab === "overrides"}
            warning={configGroupHasContent("module_tokens_override_json")}
            onClick={() => setScreenTemplateTab("overrides")}
          >
            Overrides
          </TabButton>
        </div>
        {screenTemplateTab === "general" ? (
          <div className="field-stack">
            {renderFields([
              "id",
              "production_id",
              "name",
              "screen_type",
              "module_key",
              "version",
              "default_props_json",
            ])}
          </div>
        ) : null}
        {screenTemplateTab === "behavior" && configField ? (
          <div className="field-stack single-column">
            {renderField(configField, {
              rawText: drafts.config_json ?? "{}",
              hideLabel: true,
              screenTemplateSection: "behavior",
              onRawTextChange: (nextRawText) =>
                setDrafts({
                  ...drafts,
                  config_json: nextRawText,
                }),
            })}
          </div>
        ) : null}
        {screenTemplateTab === "overrides" && configField ? (
          <div className="field-stack single-column">
            {renderField(configField, {
              rawText: drafts.config_json ?? "{}",
              hideLabel: true,
              screenTemplateSection: "overrides",
              onRawTextChange: (nextRawText) =>
                setDrafts({
                  ...drafts,
                  config_json: nextRawText,
                }),
            })}
          </div>
        ) : null}
      </section>
    );
  }

  if (table.id === "module_theme_configs") {
    const tokensField = fieldsByColumn.get("tokens_json");
    const tokenRoot = parsedObject(drafts.tokens_json ?? "{}");
    const designGroups = Object.keys(tokenRoot);
    const activeDesignGroup =
      moduleDesignGroup && designGroups.includes(moduleDesignGroup)
        ? moduleDesignGroup
        : designGroups[0] ?? "";
    const metadataRoot = parsedObject(drafts.metadata_json ?? "{}");
    const defaultTokenRoot =
      metadataRoot.default_tokens_json &&
      typeof metadataRoot.default_tokens_json === "object" &&
      !Array.isArray(metadataRoot.default_tokens_json)
        ? (metadataRoot.default_tokens_json as Record<string, unknown>)
        : {};
    const defaultTokenGroups = Object.keys(defaultTokenRoot);
    const activeNotesGroup =
      moduleNotesGroup === "notes" || defaultTokenGroups.includes(moduleNotesGroup)
        ? moduleNotesGroup
        : "notes";

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
            active={moduleThemeTab === "theme"}
            onClick={() => setModuleThemeTab("theme")}
          >
            Theme
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
                    warning={
                      JSON.stringify(tokenRoot[group]) !==
                      JSON.stringify(inheritedFields.tokens_json?.[group])
                    }
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
        {moduleThemeTab === "theme" ? (
          <div className="nested-editor-stack">
            <div className="field-stack">
              {renderFields([
                "id",
                "production_id",
                "theme_id",
                "module_id",
                "module_schema_version",
                "name",
              ])}
            </div>
            {fieldsByColumn.get("metadata_json") ? (
              <div className="nested-editor-stack">
                <div className="editor-tabs subtle-tabs">
                  {["notes", ...defaultTokenGroups]
                    .filter((value, index, all) => value && all.indexOf(value) === index)
                    .map((group) => (
                      <TabButton
                        key={group}
                        active={activeNotesGroup === group}
                        warning={
                          group !== "notes" &&
                          Boolean(
                            parsedObject(drafts.metadata_json ?? "{}")
                              .default_tokens_json,
                          )
                        }
                        onClick={() => setModuleNotesGroup(group)}
                      >
                        {group === "notes" ? "Notes" : friendlyGroupLabel(group)}
                      </TabButton>
                    ))}
                </div>
                <div className="field-stack single-column">
                  {renderField(fieldsByColumn.get("metadata_json")!, {
                    rawText: rawForModuleNotesGroup(activeNotesGroup),
                    hideLabel: true,
                    groupContext:
                      activeNotesGroup === "notes" ? undefined : activeNotesGroup,
                    onRawTextChange: (nextRawText) =>
                      updateModuleNotesGroup(activeNotesGroup, nextRawText),
                  })}
                </div>
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
