import type { Dispatch, SetStateAction } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppState,
  AppTableDefinition,
} from "../api/client.js";
import { renderGenericField as renderGenericFieldFromDispatcher } from "./GenericFieldDispatcher.js";
import {
  RecordFieldRenderer,
  type RawJsonFieldOverride,
} from "./RecordFieldRenderer.js";
import {
  stringifyJson,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import type { ProductionFontCatalog } from "../components/json-editor/productionFonts.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";

type SaveState = "saved" | "dirty" | "invalid" | "saving" | "failed";

interface NativeBridge {
  pickFile?: () => Promise<string[]>;
  pickDirectory?: () => Promise<string[]>;
  mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

interface RecordEditorRenderServicesOptions {
  tables: AppTableDefinition[];
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
  productionFontCatalog?: ProductionFontCatalog;
  paletteCatalog?: PaletteColorCatalog;
  onAppStateChanged?: (state: AppState, tableId: string, record: AppRecord) => void;
}

export function createRecordEditorRenderServices({
  tables,
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
  productionFontCatalog,
  paletteCatalog,
  onAppStateChanged,
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
        productionFontCatalog={productionFontCatalog}
        paletteCatalog={paletteCatalog}
        iconThemeRecords={records.icon_themes}
        mediaRoot={productionMediaRoot}
        nativeBridge={nativeBridge}
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

  function renderGenericField(field: AppFieldDefinition) {
    return renderGenericFieldFromDispatcher({
      tables,
      table,
      field,
      record,
      records,
      drafts,
      states,
      errors,
      nativeBridge,
      productionMediaRoot,
      paletteCatalog,
      onAppStateChanged,
      relativePathFromRoot,
      setDrafts,
      setJsonDraft,
      renderField,
    });
  }

  return {
    renderField,
    renderFields,
    renderGenericField,
    setJsonDraft,
  };
}
