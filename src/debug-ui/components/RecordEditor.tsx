import { useEffect, useMemo, useState } from "react";
import {
  updateAppRecord,
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { JsonTreeEditor } from "./json-editor/JsonTreeEditor.js";

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

  useEffect(() => {
    const nextDrafts = Object.fromEntries(
      table.fields.map((field) => [field.column, draftValue(record, field)]),
    );
    setDrafts(nextDrafts);
    setStates(
      Object.fromEntries(table.fields.map((field) => [field.column, "saved"])),
    );
    setErrors({});
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
        {table.fields.map((field) => {
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
              <span>
                {field.label}
                <em>{field.readonly ? "Read only" : state}</em>
              </span>
              {field.kind === "json" ? (
                <JsonTreeEditor
                  table={table}
                  field={field}
                  record={record}
                  testId={`field-${field.column}`}
                  disabled={field.readonly}
                  rawText={drafts[field.column] ?? ""}
                  inheritedValue={inheritedFields[field.column]}
                  restoreStrategy={restoreStrategyForField(table, field)}
                  allowArrayStructuralEdits={allowArrayStructuralEditsForField(
                    table,
                    field,
                  )}
                  onRawTextChange={(nextRawText) =>
                    setDrafts({
                      ...drafts,
                      [field.column]: nextRawText,
                    })
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
        })}
      </div>
    </section>
  );
}
