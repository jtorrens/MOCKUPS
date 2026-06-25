import type { Dispatch, SetStateAction } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../api/client.js";
import { renderGenericField as renderGenericFieldFromDispatcher } from "./GenericFieldDispatcher.js";
import {
  DeviceMetricsField,
  FlatJsonObjectEditor,
} from "./FlatJsonFieldEditors.js";
import {
  RecordFieldRenderer,
  type RawJsonFieldOverride,
} from "./RecordFieldRenderer.js";
import {
  parsedJsonValue,
  parsedObject,
} from "./recordJsonUtils.js";
import {
  stringifyJson,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";

type SaveState = "saved" | "dirty" | "invalid" | "saving" | "failed";

interface NativeBridge {
  pickFile?: () => Promise<string[]>;
  pickDirectory?: () => Promise<string[]>;
  mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

interface RecordEditorRenderServicesOptions {
  table: AppTableDefinition;
  record: AppRecord;
  records: Record<string, AppRecord[]>;
  fieldsByColumn: Map<string, AppFieldDefinition>;
  drafts: Record<string, string>;
  states: Record<string, SaveState>;
  errors: Record<string, string>;
  inheritedFields: Record<string, Record<string, unknown>>;
  nativeBridge: NativeBridge | undefined;
  productionMediaRoot: string;
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  setDrafts: Dispatch<SetStateAction<Record<string, string>>>;
}

export function createRecordEditorRenderServices({
  table,
  record,
  records,
  fieldsByColumn,
  drafts,
  states,
  errors,
  inheritedFields,
  nativeBridge,
  productionMediaRoot,
  relativePathFromRoot,
  setDrafts,
}: RecordEditorRenderServicesOptions) {
  function setJsonDraft(column: string, value: JsonValue) {
    setDrafts({
      ...drafts,
      [column]: stringifyJson(value),
    });
  }

  function renderField(
    field: AppFieldDefinition,
    rawOverride?: RawJsonFieldOverride,
  ) {
    return (
      <RecordFieldRenderer
        key={field.column}
        table={table}
        field={field}
        record={record}
        records={records}
        drafts={drafts}
        state={states[field.column] ?? "saved"}
        error={errors[field.column]}
        inheritedValue={inheritedFields[field.column]}
        rawOverride={rawOverride}
        onDraftChange={(column, value) =>
          setDrafts({
            ...drafts,
            [column]: value,
          })
        }
      />
    );
  }

  function renderFields(columns: string[]) {
    return columns
      .map((column) => fieldsByColumn.get(column))
      .filter((field): field is AppFieldDefinition => Boolean(field))
      .filter((field) => field.column !== "id")
      .map((field) => renderField(field));
  }

  function renderFlatJsonObjectEditor(
    column: string,
    omitKeys: string[] = [],
  ) {
    const field = fieldsByColumn.get(column);
    if (!field) return null;
    const root = parsedObject(drafts[column] ?? "{}");
    return (
      <FlatJsonObjectEditor
        table={table}
        field={field}
        record={record}
        root={root}
        omitKeys={omitKeys}
        onRootChange={(nextRoot) => setJsonDraft(column, nextRoot)}
      />
    );
  }

  function renderDeviceMetricsField(field: AppFieldDefinition) {
    const root = parsedJsonValue(drafts[field.column] ?? "{}", {}) as JsonValue;
    return (
      <DeviceMetricsField
        key={field.column}
        table={table}
        field={field}
        record={record}
        root={root}
        onRootChange={(nextRoot) => setJsonDraft(field.column, nextRoot)}
      />
    );
  }

  function renderGenericField(field: AppFieldDefinition) {
    return renderGenericFieldFromDispatcher({
      table,
      field,
      record,
      records,
      drafts,
      states,
      errors,
      nativeBridge,
      productionMediaRoot,
      relativePathFromRoot,
      setDrafts,
      setJsonDraft,
      renderField,
      renderFlatJsonObjectEditor,
      renderDeviceMetricsField,
    });
  }

  return {
    renderDeviceMetricsField,
    renderField,
    renderFields,
    renderFlatJsonObjectEditor,
    renderGenericField,
    setJsonDraft,
  };
}
