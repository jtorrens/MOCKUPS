import { useMemo, useState } from "react";
import type { FieldDefinition } from "../../domain/value-system/index.js";
import {
  DictionaryFieldControl,
  DICTIONARY_FIELD_CLASS,
  type DictionaryFileBrowser,
  type DictionarySelectOptions,
} from "../editor-ui/DictionaryFieldControl.js";
import type { IconThemeLikeRecord } from "../editor-ui/IconGlyphPreview.js";
import type { PaletteColorCatalog } from "./json-editor/paletteColors.js";
import type { ProductionFontCatalog } from "./json-editor/productionFonts.js";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "./inspector/InspectorFieldRow.js";

export interface ComponentOverrideField {
  readonly key: string;
  readonly field: FieldDefinition;
  readonly selectOptions?: DictionarySelectOptions;
}

interface ComponentOverrideModalProps {
  title: string;
  componentName: string;
  fields: ComponentOverrideField[];
  baseTokens: Record<string, unknown>;
  overrides: Record<string, unknown>;
  paletteCatalog?: PaletteColorCatalog;
  productionFontCatalog?: ProductionFontCatalog;
  iconThemeRecords?: readonly IconThemeLikeRecord[];
  mediaRoot?: string;
  fileBrowser?: DictionaryFileBrowser;
  onCancel: () => void;
  onApply: (nextOverrides: Record<string, unknown>) => void;
}

function hasOwnKey(value: Record<string, unknown>, key: string) {
  return Object.prototype.hasOwnProperty.call(value, key);
}

function jsonStable(value: unknown) {
  return JSON.stringify(value ?? null);
}

function changedFromBase(
  key: string,
  baseTokens: Record<string, unknown>,
  overrides: Record<string, unknown>,
) {
  if (!hasOwnKey(overrides, key)) return false;
  return jsonStable(overrides[key]) !== jsonStable(baseTokens[key]);
}

function cleanOverrides(
  fields: ComponentOverrideField[],
  baseTokens: Record<string, unknown>,
  overrides: Record<string, unknown>,
) {
  const allowed = new Set(fields.map((field) => field.key));
  return Object.fromEntries(
    Object.entries(overrides).filter(
      ([key, value]) =>
        allowed.has(key) &&
        jsonStable(value) !== jsonStable(baseTokens[key]),
    ),
  );
}

export function ComponentOverrideModal({
  title,
  componentName,
  fields,
  baseTokens,
  overrides,
  paletteCatalog,
  productionFontCatalog,
  iconThemeRecords,
  mediaRoot,
  fileBrowser,
  onCancel,
  onApply,
}: ComponentOverrideModalProps) {
  const [draftOverrides, setDraftOverrides] = useState<Record<string, unknown>>(
    () => cleanOverrides(fields, baseTokens, overrides),
  );
  const cleanedDraft = useMemo(
    () => cleanOverrides(fields, baseTokens, draftOverrides),
    [baseTokens, draftOverrides, fields],
  );
  const hasChanges = jsonStable(cleanedDraft) !== jsonStable(cleanOverrides(fields, baseTokens, overrides));

  function setOverride(key: string, value: unknown) {
    setDraftOverrides((current) => {
      const next = { ...current, [key]: value };
      if (jsonStable(value) === jsonStable(baseTokens[key])) {
        delete next[key];
      }
      return next;
    });
  }

  function restoreOverride(key: string) {
    setDraftOverrides((current) => {
      const next = { ...current };
      delete next[key];
      return next;
    });
  }

  return (
    <div className="modal-backdrop" role="presentation" onMouseDown={onCancel}>
      <section
        className="app-modal-card app-confirm-modal component-override-modal"
        role="dialog"
        aria-modal="true"
        aria-label={`${title} overrides`}
        onMouseDown={(event) => event.stopPropagation()}
      >
        <div className="app-modal-heading">
          <div>
            <span className="eyebrow">Component override</span>
            <h2>{title}</h2>
          </div>
        </div>
        <p className="modal-help">
          Base component: <strong>{componentName}</strong>. Only changed fields
          are stored in this module.
        </p>
        <div className="record-editor-field-stack record-editor-single-column component-override-modal__body">
          {fields.map(({ key, field, selectOptions }) => {
            const hasOverride = changedFromBase(key, baseTokens, draftOverrides);
            const baseValue = baseTokens[key] ?? field.defaultValue;
            const value = hasOwnKey(draftOverrides, key)
              ? draftOverrides[key]
              : baseValue;
            return (
              <InspectorFieldRow
                key={key}
                className={`record-editor-field ${DICTIONARY_FIELD_CLASS} ${
                  hasOverride ? "has-override" : ""
                }`}
                state={hasOverride ? "override" : "default"}
                label={<span>{field.ui?.label ?? key}</span>}
                control={
                  <DictionaryFieldControl
                    field={field}
                    value={value}
                    localValue={hasOverride ? draftOverrides[key] : undefined}
                    parentValue={baseValue}
                    defaultValue={baseValue}
                    selectOptions={selectOptions}
                    paletteCatalog={paletteCatalog}
                    productionFontCatalog={productionFontCatalog}
                    iconThemeRecords={iconThemeRecords}
                    mediaRoot={mediaRoot}
                    fileBrowser={fileBrowser}
                    onChange={(nextValue) => setOverride(key, nextValue)}
                  />
                }
                restore={
                  hasOverride ? (
                    <InspectorRestoreButton
                      label={`Restore ${field.ui?.label ?? key}`}
                      onClick={() => restoreOverride(key)}
                    />
                  ) : undefined
                }
              />
            );
          })}
        </div>
        <footer className="app-modal-actions">
          <button type="button" className="app-modal-button" onClick={onCancel}>
            Cancel
          </button>
          <button
            type="button"
            className="app-modal-button primary"
            disabled={!hasChanges}
            onClick={() => onApply(cleanedDraft)}
          >
            Apply
          </button>
        </footer>
      </section>
    </div>
  );
}
