import { useState, type ReactNode } from "react";
import {
  renamePaletteColorToken,
  type AppFieldDefinition,
  type AppRecord,
  type AppState,
  type AppTableDefinition,
} from "../api/client.js";
import { JsonValueEditor } from "../components/json-editor/JsonValueEditor.js";
import { ColorValueEditor } from "../components/json-editor/ColorValueEditor.js";
import { DeferredTextInput } from "../editor-ui/DeferredTextInput.js";
import {
  setAtPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { buildJsonUiHints } from "../components/json-editor/uiHints.js";
import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import { ActorMetadataFields } from "./ActorFields.js";
import { ActorAvatarPreview } from "./MediaPreviews.js";
import type { PaletteColorCatalog } from "../components/json-editor/paletteColors.js";
import { ProductionSettingsField } from "./ProductionFields.js";
import { renderRenderPresetField } from "./RenderPresetFields.js";
import { renderShotSpecialField } from "./ShotFields.js";
import { parsedObject } from "./recordJsonUtils.js";
import type { FieldSaveState } from "./RecordFieldRenderer.js";
import {
  paletteTokenUsageCount,
  paletteTokenUsages,
} from "./paletteUsage.js";

interface GenericFieldDispatcherContext {
  tables: AppTableDefinition[];
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
  paletteCatalog?: PaletteColorCatalog;
  relativePathFromRoot: (filePath: string, rootPath: string) => string;
  setDrafts: (nextDrafts: Record<string, string>) => void;
  onAppStateChanged?: (state: AppState, tableId: string, record: AppRecord) => void;
  setJsonDraft: (column: string, value: JsonValue) => void;
  renderField: (field: AppFieldDefinition) => ReactNode;
  renderFlatJsonObjectEditor: (
    column: string,
    omitKeys?: string[],
  ) => ReactNode;
  renderDeviceMetricsField: (field: AppFieldDefinition) => ReactNode;
}

function PaletteTokenField({
  field,
  record,
  records,
  tables,
  drafts,
  states,
  errors,
  setDrafts,
  onAppStateChanged,
}: {
  field: AppFieldDefinition;
  record: AppRecord | undefined;
  records: Record<string, AppRecord[]>;
  tables: AppTableDefinition[];
  drafts: Record<string, string>;
  states: Record<string, FieldSaveState>;
  errors: Record<string, string>;
  setDrafts: (nextDrafts: Record<string, string>) => void;
  onAppStateChanged?: (state: AppState, tableId: string, record: AppRecord) => void;
}) {
  const token = drafts[field.column] ?? "";
  const usages = paletteTokenUsages({ tables, records, record, token });
  const usageCount = paletteTokenUsageCount(usages);
  const isUsed = usageCount > 0;
  const [isRenameOpen, setRenameOpen] = useState(false);
  const [nextToken, setNextToken] = useState(token);
  const [renameError, setRenameError] = useState("");
  const [isRenaming, setRenaming] = useState(false);

  function openRenameModal() {
    setNextToken(token);
    setRenameError("");
    setRenameOpen(true);
  }

  async function commitRename() {
    const trimmed = nextToken.trim();
    if (!trimmed || trimmed === token) {
      setRenameOpen(false);
      return;
    }
    setRenaming(true);
    setRenameError("");
    try {
      const result = await renamePaletteColorToken({
        recordId: record?.id ?? "",
        nextToken: trimmed,
      });
      onAppStateChanged?.(result.state, result.tableId, result.record);
      setRenameOpen(false);
    } catch (error) {
      setRenameError(error instanceof Error ? error.message : String(error));
    } finally {
      setRenaming(false);
    }
  }

  return (
    <>
      <InspectorFieldRow
        key={field.column}
        className={`record-editor-field record-editor-field-string palette-token-field state-${states[field.column] ?? "saved"}`}
        state={errors[field.column] ? "invalid" : "default"}
        label={<span>{field.label}</span>}
        meta={
          <small className="palette-token-usage">
            {isUsed
              ? `Used in ${usageCount} place${usageCount === 1 ? "" : "s"}`
              : "Unused"}
          </small>
        }
        control={
          <div className="palette-token-control">
            <DeferredTextInput
              ariaLabel={field.label}
              disabled={isUsed}
              value={token}
              onCommit={(nextValue) =>
                setDrafts({
                  ...drafts,
                  [field.column]: nextValue,
                })
              }
            />
            {isUsed ? (
              <button
                type="button"
                className="record-editor-compact-button"
                onClick={openRenameModal}
              >
                Rename…
              </button>
            ) : null}
          </div>
        }
      />
      {isRenameOpen ? (
        <div
          className="modal-backdrop palette-modal-backdrop"
          role="presentation"
          onMouseDown={() => setRenameOpen(false)}
        >
          <section
            className="app-modal-card palette-token-modal"
            role="dialog"
            aria-modal="true"
            aria-label="Rename palette token"
            onMouseDown={(event) => event.stopPropagation()}
          >
            <div className="app-modal-heading">
              <div>
                <span className="eyebrow">Palette token</span>
                <h2>Rename “{token}”</h2>
              </div>
              <button
                type="button"
                className="app-modal-close-button"
                onClick={() => setRenameOpen(false)}
              >
                Cancel
              </button>
            </div>
            <p className="modal-help">
              This will rename the token and update every exact reference in this
              production.
            </p>
            <label className="palette-modal-field">
              <span>New token name</span>
              <input
                autoFocus
                spellCheck={false}
                value={nextToken}
                onChange={(event) => setNextToken(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") {
                    void commitRename();
                  }
                }}
              />
            </label>
            <div className="palette-usage-list">
              {usages.map((usage) => (
                <div
                  key={`${usage.tableId}:${usage.recordId}:${usage.field}`}
                  className="palette-usage-row"
                >
                  <strong>{usage.tableLabel}</strong>
                  <span>{usage.recordLabel}</span>
                  <small>
                    {usage.field} · {usage.count} reference
                    {usage.count === 1 ? "" : "s"}
                  </small>
                </div>
              ))}
            </div>
            {renameError ? <p className="form-error">{renameError}</p> : null}
            <footer className="palette-modal-actions">
              <button
                type="button"
                className="secondary-button"
                disabled={isRenaming}
                onClick={() => setRenameOpen(false)}
              >
                Cancel
              </button>
              <button
                type="button"
                className="primary-button"
                disabled={isRenaming}
                onClick={() => {
                  void commitRename();
                }}
              >
                Rename and update references
              </button>
            </footer>
          </section>
        </div>
      ) : null}
    </>
  );
}

export function renderGenericField(
  context: GenericFieldDispatcherContext,
): ReactNode {
  const {
    table,
    tables,
    field,
    record,
    records,
    drafts,
    states,
    errors,
    nativeBridge,
    productionMediaRoot,
    paletteCatalog,
    relativePathFromRoot,
    setDrafts,
    onAppStateChanged,
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
  if (table.id === "palette_colors" && field.column === "token") {
    return (
      <PaletteTokenField
        field={field}
        record={record}
        records={records}
        tables={tables}
        drafts={drafts}
        states={states}
        errors={errors}
        setDrafts={setDrafts}
        onAppStateChanged={onAppStateChanged}
      />
    );
  }
  if (table.id === "palette_colors" && field.column === "value_hex") {
    const value = drafts[field.column] ?? "";
    return (
      <InspectorFieldRow
        key={field.column}
        className={`record-editor-field record-editor-field-string palette-color-value-field state-${states[field.column] ?? "saved"}`}
        state={errors[field.column] ? "invalid" : "default"}
        label={<span>{field.label}</span>}
        control={
          <div className="palette-color-value-control palette-color-free-control">
            <ColorValueEditor
              label={field.label}
              value={value}
              onChange={(nextValue) =>
                setDrafts({
                  ...drafts,
                  [field.column]: nextValue.toUpperCase(),
                })
              }
            />
            <span
              className="palette-color-editor-swatch"
              style={{ backgroundColor: value || "transparent" }}
              aria-hidden="true"
            />
          </div>
        }
      />
    );
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
        paletteCatalog={paletteCatalog}
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
                  paletteCatalog={paletteCatalog}
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
