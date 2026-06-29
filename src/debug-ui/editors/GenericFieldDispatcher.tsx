import type { ReactNode } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppState,
  AppTableDefinition,
} from "../api/client.js";
import type { JsonValue } from "../components/json-editor/jsonEditorUtils.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import type { FieldSaveState } from "./RecordFieldRenderer.js";

interface GenericFieldDispatcherContext {
  tables: AppTableDefinition[];
  table: AppTableDefinition;
  field: AppFieldDefinition;
  record: AppRecord | undefined;
  records: Record<string, AppRecord[]>;
  drafts: Record<string, string>;
  states: Record<string, FieldSaveState>;
  errors: Record<string, string>;
  nativeBridge:
    | {
        pickFile?: () => Promise<string[]>;
        pickDirectory?: () => Promise<string[]>;
      }
    | undefined;
  productionMediaRoot: string;
  paletteCatalog?: PaletteColorCatalog;
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  setDrafts: (nextDrafts: Record<string, string>) => void;
  onAppStateChanged?: (state: AppState, tableId: string, record: AppRecord) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
  renderField: (field: AppFieldDefinition) => ReactNode;
}

export function renderGenericField({
  table,
  field,
  renderField,
}: GenericFieldDispatcherContext): ReactNode {
  if (
    table.id === "devices" &&
    ["production_id", "manufacturer", "model", "os_family"].includes(field.column)
  ) {
    return null;
  }
  if (table.id === "actors" && field.column === "production_id") return null;
  if (
    table.id === "episodes" &&
    (field.column === "production_id" || field.column === "sort_order")
  ) {
    return null;
  }
  if (field.column === "id") return null;
  return renderField(field);
}
