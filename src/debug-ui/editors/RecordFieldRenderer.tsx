import {
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { JsonTreeEditor } from "../components/json-editor/JsonTreeEditor.js";
import type { ProductionFontCatalog } from "../components/json-editor/productionFonts.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import { fieldDefinitionForRecordColumn } from "../../domain/fields/recordColumnFields.js";
import {
  controlDefinitionForField,
  editorMetadataForField,
} from "../editor-ui/ValueKindControlRegistry.js";
import type { FieldDefinition } from "../../domain/value-system/index.js";
import {
  DictionaryFieldControl,
  DICTIONARY_FIELD_CLASS,
} from "../editor-ui/DictionaryFieldControl.js";
import {
  EditorFieldRow,
} from "../editor-ui/fields/EditorFieldRow.js";
import {
  toDictionaryFieldControlProps,
  type EditorFieldState,
} from "../editor-ui/fields/EditorFieldDescriptor.js";
import {
  createRecordFieldDescriptor,
} from "../editor-ui/fields/createRecordFieldDescriptor.js";

export type FieldSaveState =
  | "saved"
  | "dirty"
  | "invalid"
  | "saving"
  | "failed";

export interface RawJsonFieldOverride {
  rawText: string;
  onRawTextChange: (nextRawText: string) => void;
  inheritedValue?: Record<string, unknown> | null;
  groupContext?: string;
  hideLabel?: boolean;
}

export function titleForRecord(record: AppRecord, fallbackColumn: string) {
  return String(record[fallbackColumn] ?? record.name ?? record.id);
}

function restoreStrategyForField(
  table: AppTableDefinition,
  field: AppFieldDefinition,
): "remove" | "set" {
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
  return table.id === "module_instances" && field.column === "content_json";
}

function objectRecord(value: unknown): Record<string, unknown> | undefined {
  return value !== null && typeof value === "object" && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : undefined;
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
    const productionId = record?.production_id;
    return {
      allowEmpty: table.id === "shots" ? false : Boolean(field.nullable),
      options: records.episodes
        ?.filter(
          (item) =>
            !productionId ||
            !Object.hasOwn(item, "production_id") ||
            item.production_id === productionId,
        )
        .map((item) => ({
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
  if (field.column === "icon_theme_id") {
    const productionId = record?.production_id;
    return {
      allowEmpty: true,
      options: records.icon_themes
        ?.filter(
          (item) =>
            !productionId ||
            !Object.hasOwn(item, "production_id") ||
            item.production_id === productionId,
        )
        .map((item) => ({
          value: item.id,
          label: titleForRecord(item, "name"),
        })) ?? [],
    };
  }
  if (field.column === "status_bar_id") {
    const productionId = record?.production_id;
    return {
      allowEmpty: true,
      options: records.status_bars
        ?.filter(
          (item) =>
            !productionId ||
            !Object.hasOwn(item, "production_id") ||
            item.production_id === productionId,
        )
        .map((item) => ({
          value: item.id,
          label: titleForRecord(item, "name"),
        })) ?? [],
    };
  }
  if (field.column === "navigation_bar_id") {
    const productionId = record?.production_id;
    return {
      allowEmpty: true,
      options: records.navigation_bars
        ?.filter(
          (item) =>
            !productionId ||
            !Object.hasOwn(item, "production_id") ||
            item.production_id === productionId,
        )
        .map((item) => ({
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
  if (field.column === "default_device_id") {
    return {
      allowEmpty: true,
      options: records.devices?.map((item) => ({
        value: item.id,
        label: titleForRecord(item, "name"),
      })) ?? [],
    };
  }
  if (field.column === "default_theme_id") {
    return {
      allowEmpty: true,
      options: records.themes?.map((item) => ({
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

function dictionaryFieldForColumn(
  table: AppTableDefinition,
  field: AppFieldDefinition,
): FieldDefinition | undefined {
  return fieldDefinitionForRecordColumn(table.id, field.column);
}

function relationOptionsForDictionaryField(
  field: FieldDefinition,
  record: AppRecord | undefined,
  records: Record<string, AppRecord[]>,
): { options: { value: string; label: string }[]; allowEmpty: boolean } | undefined {
  const metadata = editorMetadataForField(field);
  if (!metadata.tableId) return undefined;
  const productionId =
    typeof record?.production_id === "string" ? record.production_id : undefined;
  const tableRecords = (records[metadata.tableId] ?? []).filter(
    (item) =>
      !productionId ||
      !Object.hasOwn(item, "production_id") ||
      item.production_id === productionId,
  );
  const labelColumn = metadata.labelColumn ?? "name";
  return {
    allowEmpty: metadata.allowEmpty === true,
    options: tableRecords.map((item) => ({
      value: item.id,
      label: titleForRecord(item, labelColumn),
    })),
  };
}

function draftValueForDictionaryWrite(value: unknown): string {
  if (typeof value === "boolean") return value ? "1" : "0";
  return value === undefined || value === null ? "" : String(value);
}

function descriptorStateForSaveState(
  state: FieldSaveState,
  error: string | undefined,
): EditorFieldState {
  if (error || state === "invalid" || state === "failed") return "invalid";
  return "default";
}

interface RecordFieldRendererProps {
  table: AppTableDefinition;
  field: AppFieldDefinition;
  record: AppRecord | undefined;
  records: Record<string, AppRecord[]>;
  drafts: Record<string, string>;
  state: FieldSaveState;
  error: string | undefined;
  inheritedValue: unknown;
  rawOverride?: RawJsonFieldOverride;
  productionFontCatalog?: ProductionFontCatalog;
  paletteCatalog?: PaletteColorCatalog;
  mediaRoot?: string;
  nativeBridge?: {
    pickFile?: () => Promise<string[]>;
    pickDirectory?: () => Promise<string[]>;
    mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
  };
  onDraftChange: (column: string, value: string) => void;
}

export function RecordFieldRenderer({
  table,
  field,
  record,
  records,
  drafts,
  state,
  error,
  inheritedValue,
  rawOverride,
  productionFontCatalog,
  paletteCatalog,
  mediaRoot,
  nativeBridge,
  onDraftChange,
}: RecordFieldRendererProps) {
  const dictionaryField = dictionaryFieldForColumn(table, field);
  const dictionaryClassName = dictionaryField ? ` ${DICTIONARY_FIELD_CLASS}` : "";
  const dictionaryMetadata = dictionaryField
    ? editorMetadataForField(dictionaryField)
    : undefined;
  const dictionaryControl = dictionaryField
    ? controlDefinitionForField(dictionaryField).control
    : undefined;
  const relationSelect =
    dictionaryField && dictionaryControl === "recordSelect"
      ? relationOptionsForDictionaryField(dictionaryField, record, records)
      : relationOptionsForField(
          table,
          field,
          record,
          records,
        );
  const dictionarySelect =
    dictionaryField &&
    dictionaryControl === "select" &&
    dictionaryMetadata?.options?.length
      ? {
          allowEmpty: dictionaryMetadata.allowEmpty === true,
          options: dictionaryMetadata.options.map((option) => ({
            value: option,
            label: option,
          })),
        }
      : undefined;
  const selectOptions = relationSelect ?? dictionarySelect;

  if (field.kind !== "json") {
    if (dictionaryField) {
      const descriptor = createRecordFieldDescriptor({
        field: dictionaryField,
        column: field.column,
        value: drafts[field.column] ?? "",
        recordId: typeof record?.id === "string" ? record.id : undefined,
        recordType: table.id,
        readonly: field.readonly,
        error,
        selectOptions: selectOptions
          ? {
              ...selectOptions,
              emptyLabel: [
                "default_device_id",
                "default_theme_id",
              ].includes(field.column)
                ? "None"
                : "Inherited/default",
            }
          : undefined,
        state: descriptorStateForSaveState(state, error),
        onWrite: (nextValue) =>
          onDraftChange(field.column, draftValueForDictionaryWrite(nextValue)),
      });

      return (
        <EditorFieldRow
          key={field.column}
          className={`record-editor-field record-editor-field-${field.kind} state-${state} ${DICTIONARY_FIELD_CLASS} ${
            field.readonly ? "is-readonly" : ""
          }`}
          descriptor={descriptor}
        >
          <DictionaryFieldControl
            {...toDictionaryFieldControlProps(descriptor, {
              fileBrowser: nativeBridge,
              mediaRoot,
              paletteCatalog,
              productionFontCatalog,
            })}
          />
        </EditorFieldRow>
      );
    }

    return (
      <InspectorFieldRow
        key={field.column}
        className={`record-editor-field record-editor-field-${field.kind} state-${state} ${
          field.readonly ? "is-readonly" : ""
        }`}
        state={state === "invalid" || state === "failed" ? "invalid" : "default"}
        label={<span>{field.label}</span>}
        control={
          <>
            {error ? <strong>{error}</strong> : null}
          </>
        }
      />
    );
  }

  return (
    <div
      key={field.column}
      className={`record-editor-field record-editor-field-${field.kind} state-${state}${dictionaryClassName} ${
        rawOverride?.hideLabel && field.kind === "json"
          ? "record-editor-field-frameless"
          : ""
      }`}
    >
      {rawOverride?.hideLabel || field.kind === "json" ? null : (
        <span>{field.label}</span>
      )}
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
            : objectRecord(inheritedValue)
        }
        restoreStrategy={restoreStrategyForField(table, field)}
        groupContext={rawOverride?.groupContext}
        allowArrayStructuralEdits={allowArrayStructuralEditsForField(
          table,
          field,
        )}
        productionFontCatalog={productionFontCatalog}
        paletteCatalog={paletteCatalog}
        mediaRoot={mediaRoot}
        nativeBridge={nativeBridge}
        onRawTextChange={
          rawOverride?.onRawTextChange ??
          ((nextRawText) => onDraftChange(field.column, nextRawText))
        }
      />
      {error ? <strong>{error}</strong> : null}
    </div>
  );
}
