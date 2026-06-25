import type { ReactNode } from "react";
import {
  type AppFieldDefinition,
  type AppRecord,
  type AppTableDefinition,
} from "../api/client.js";
import { JsonValueEditor } from "../components/json-editor/JsonValueEditor.js";
import {
  setAtPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { buildJsonUiHints } from "../components/json-editor/uiHints.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import { ActorMetadataFields } from "./ActorFields.js";
import { ActorAvatarPreview } from "./MediaPreviews.js";
import { ProductionSettingsField } from "./ProductionFields.js";
import { renderRenderPresetField } from "./RenderPresetFields.js";
import { renderShotSpecialField } from "./ShotFields.js";
import { parsedObject } from "./recordJsonUtils.js";
import type { FieldSaveState } from "./RecordFieldRenderer.js";

interface GenericFieldDispatcherContext {
  table: AppTableDefinition;
  field: AppFieldDefinition;
  record: AppRecord | undefined;
  records: Record<string, AppRecord[]>;
  drafts: Record<string, string>;
  states: Record<string, FieldSaveState>;
  errors: Record<string, string>;
  nativeBridge: {
    pickFile?: () => Promise<string[]>;
    pickDirectory?: () => Promise<string[]>;
  } | undefined;
  productionMediaRoot: string;
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  setDrafts: (nextDrafts: Record<string, string>) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
  renderField: (field: AppFieldDefinition) => ReactNode;
  renderFlatJsonObjectEditor: (
    column: string,
    omitKeys?: string[],
  ) => ReactNode;
  renderDeviceMetricsField: (field: AppFieldDefinition) => ReactNode;
}

export function renderGenericField(
  context: GenericFieldDispatcherContext,
): ReactNode {
  const {
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
  } = context;

  if (table.id === "productions" && field.column === "settings_json") {
    return (
      <ProductionSettingsField
        field={field}
        rawValue={drafts[field.column] ?? "{}"}
        nativeBridge={nativeBridge}
        onChange={setJsonDraft}
      />
    );
  }
  if (table.id === "render_presets") {
    return renderRenderPresetField({
      field,
      drafts,
      setDrafts,
      setJsonDraft,
      renderField,
    });
  }
  if (
    table.id === "devices" &&
    ["production_id", "manufacturer", "model", "os_family"].includes(field.column)
  ) {
    return null;
  }
  if (table.id === "devices" && field.column === "metrics_json") {
    return renderDeviceMetricsField(field);
  }
  if (table.id === "actors" && field.column === "production_id") {
    return null;
  }
  if (table.id === "actors" && field.column === "avatar_asset_id") {
    return null;
  }
  if (table.id === "actors" && field.column === "metadata_json") {
    return (
      <ActorMetadataFields
        record={record}
        drafts={drafts}
        mediaRoot={productionMediaRoot}
        nativeBridge={nativeBridge}
        relativePathFromRoot={relativePathFromRoot}
        AvatarPreview={ActorAvatarPreview}
        setMetadataRaw={(nextRaw) =>
          setDrafts({
            ...drafts,
            metadata_json: nextRaw,
          })
        }
      />
    );
  }
  if (table.id === "shots") {
    const shotField = renderShotSpecialField({
      field,
      records,
      record,
      drafts,
      states,
      errors,
      setDraftValue: (column, value) =>
        setDrafts({
          ...drafts,
          [column]: value,
        }),
      renderField,
    });
    if (shotField !== undefined) return shotField;
  }
  if (
    table.id === "episodes" &&
    (field.column === "production_id" || field.column === "sort_order")
  ) {
    return null;
  }
  if (field.column === "id") return null;
  if (field.kind === "json") {
    const root = parsedObject(drafts[field.column] ?? "{}");
    const visibleKeys = Object.keys(root).filter((key) => key !== "source");
    if (Object.keys(root).length === 0 || visibleKeys.length === 0) {
      if (table.id === "shots" && field.column === "metadata_json") {
        return (
          <div key={field.column} className="flat-json-field-group">
            <InspectorFieldRow
              className="record-editor-field flat-json-row"
              label={<span>Note</span>}
              control={
                <JsonValueEditor
                  rootValue={{} as JsonValue}
                  path={["note"]}
                  value=""
                  hints={buildJsonUiHints(table, field, record)}
                  onRootChange={(nextRoot) =>
                    setJsonDraft(field.column, nextRoot)
                  }
                  onChange={(nextValue) =>
                    setJsonDraft(
                      field.column,
                      setAtPath({} as JsonValue, ["note"], nextValue),
                    )
                  }
                />
              }
            />
          </div>
        );
      }
      return null;
    }
    return (
      <div key={field.column} className="flat-json-field-group">
        {renderFlatJsonObjectEditor(field.column, ["source"])}
      </div>
    );
  }
  return renderField(field);
}
