import { useState } from "react";
import { DeferredNumberInput } from "../../editor-ui/DeferredNumberInput.js";
import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import {
  DictionaryFieldControl,
  DICTIONARY_FIELD_CLASS,
} from "../../editor-ui/DictionaryFieldControl.js";
import { EditorSubsectionAccordion } from "../../editor-ui/EditorSubsectionAccordion.js";
import {
  controlDefinitionForField,
} from "../../editor-ui/ValueKindControlRegistry.js";
import {
  createJsonFieldDescriptor,
} from "../../editor-ui/fields/createJsonFieldDescriptor.js";
import {
  toDictionaryFieldControlProps,
  type EditorFieldDescriptor,
} from "../../editor-ui/fields/EditorFieldDescriptor.js";
import { ColorValueEditor } from "./ColorValueEditor.js";
import {
  productionFontIdForFamily,
  type ProductionFontCatalog,
} from "./productionFonts.js";
import {
  ProductionFontSelector,
  type ProductionFontSelection,
} from "./ProductionFontSelector.js";
import type { PaletteColorCatalog } from "./paletteColors.js";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "../inspector/InspectorFieldRow.js";
import {
  compactLabelForGroup,
  friendlyGroupLabel,
  friendlyPathLeafLabel,
} from "./labels.js";
import { hintForPath, type JsonUiHint } from "./uiHints.js";
import {
  deleteAtPathAndPrune,
  deepEqualJson,
  getAtPath,
  hasAtPath,
  isJsonObject,
  pathLabel,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "./jsonEditorUtils.js";
import type { JsonUiHints } from "./uiHints.js";

interface TokenOverrideNativeBridge {
  pickFile?: () => Promise<string[]>;
  pickDirectory?: () => Promise<string[]>;
  mediaDataUrl?: (filePath: string, rootPath: string) => Promise<string>;
}

interface TokenOverrideEditorProps {
  rootValue: JsonValue;
  inheritedRoot: JsonValue;
  hints: JsonUiHints;
  showInheritedValue?: boolean;
  inheritedColumnLabel?: string;
  groupContext?: string;
  restoreMode?: "remove" | "set";
  productionFontCatalog?: ProductionFontCatalog;
  paletteCatalog?: PaletteColorCatalog;
  mediaRoot?: string;
  nativeBridge?: TokenOverrideNativeBridge;
  onRootChange: (nextValue: JsonValue) => void;
}

interface TokenRow {
  path: JsonPath;
  value: JsonValue;
}

interface TokenRowGroup {
  key: string;
  label: string;
  rows: TokenRow[];
  renderAsGroup: boolean;
}

function flattenPrimitiveTokens(value: JsonValue, path: JsonPath = []): TokenRow[] {
  if (Array.isArray(value)) {
    return value.flatMap((entry, index) =>
      flattenPrimitiveTokens(entry, [...path, index]),
    );
  }
  if (isJsonObject(value)) {
    return Object.entries(value).flatMap(([key, entry]) =>
      key === "productionFontId" || key === "source"
        ? []
        : flattenPrimitiveTokens(entry, [...path, key]),
    );
  }
  return [{ path, value }];
}

function pathFromHintKey(key: string): JsonPath {
  return key.split(".").filter(Boolean);
}

function rowsWithDictionaryDefaults(
  rows: TokenRow[],
  hints: JsonUiHints,
): TokenRow[] {
  const byPath = new Map(rows.map((row) => [pathLabel(row.path), row]));
  for (const [key, hint] of Object.entries(hints)) {
    if (!hint.field) continue;
    const path = pathFromHintKey(hint.canonicalPath ?? key);
    if (path.length === 0) continue;
    const pathKey = pathLabel(path);
    if (byPath.has(pathKey)) continue;
    byPath.set(pathKey, {
      path,
      value: (hint.field.defaultValue ?? "") as JsonValue,
    });
  }
  return Array.from(byPath.values());
}

function tokenDisplayValue(value: JsonValue): string {
  if (value === null) return "null";
  if (typeof value === "string") return value;
  return String(value);
}

function parseOverride(raw: string, inheritedValue: JsonValue): JsonValue | null {
  if (typeof inheritedValue === "number") {
    const next = Number(raw);
    return Number.isFinite(next) ? next : null;
  }
  if (typeof inheritedValue === "boolean") {
    return raw === "true";
  }
  return raw;
}

function isHexColor(value: string): boolean {
  return /^#[0-9a-fA-F]{6}$/.test(value.trim());
}

function isRgbColor(value: string): boolean {
  return /^rgb\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*\)$/i.test(
    value.trim(),
  );
}

function isRgbaColor(value: string): boolean {
  return value.trim().toLowerCase().startsWith("rgba(");
}

function isColorLikeKey(key: string) {
  return /color|background|text|accent|foreground/i.test(key);
}

function widgetForRow(
  hint: JsonUiHint,
  key: string,
  value: JsonValue,
  groupContext?: string,
) {
  if (hint.widget) return hint.widget;
  if (/fontFamily$/i.test(key) || (key === "family" && /font|fonts/i.test(groupContext ?? ""))) {
    return "font";
  }
  if (/Weight$/i.test(key)) return "select";
  if (typeof value === "number") return "number";
  if (typeof value === "boolean") return "checkbox";
  if (
    typeof value === "string" &&
    (isHexColor(value) || isRgbColor(value) || isRgbaColor(value) || isColorLikeKey(key))
  ) {
    return "color";
  }
  return "text";
}

function chatBubbleGroupForRow(row: TokenRow): string {
  const firstKey = String(row.path[0] ?? "");
  if (firstKey === "tail") return "tail";
  if (firstKey === "media") return "media";
  if (firstKey === "status") return "status";
  if (firstKey.startsWith("messageLabel")) return "label";
  if (firstKey === "avatarSize" || firstKey === "avatarGap") return "avatar";
  if (
    firstKey === "paddingX" ||
    firstKey === "paddingY" ||
    firstKey === "maxWidthRatio" ||
    firstKey === "radius" ||
    firstKey === "shadowEnabled" ||
    firstKey === "surfaceReliefEnabled"
  ) {
    return "bubble";
  }
  return "general";
}

function groupedRows(rows: TokenRow[], groupContext?: string): TokenRowGroup[] {
  if (groupContext === "chatBubbles") {
    const groups = new Map<string, TokenRow[]>();
    for (const row of rows) {
      const group = chatBubbleGroupForRow(row);
      groups.set(group, [...(groups.get(group) ?? []), row]);
    }
    const order = ["bubble", "avatar", "label", "media", "tail", "status", "general"];
    return order
      .filter((key) => groups.has(key))
      .map((key) => ({
        key,
        label: friendlyGroupLabel(key),
        rows: groups.get(key) ?? [],
        renderAsGroup: true,
      }));
  }

  if (!rows.some((row) => row.path.length > 1)) {
    return [{ key: "", label: "", rows, renderAsGroup: false }];
  }
  const groups = new Map<string, TokenRow[]>();
  for (const row of rows) {
    const group = String(row.path[0] ?? "");
    groups.set(group, [...(groups.get(group) ?? []), row]);
  }
  return Array.from(groups, ([key, groupRows]) => ({
    key,
    label: friendlyGroupLabel(key),
    rows: groupRows,
    renderAsGroup: groupRows.length > 1,
  }));
}

function mergedTokenShape(inherited: JsonValue, local: JsonValue): JsonValue {
  if (isJsonObject(inherited) || isJsonObject(local)) {
    const inheritedObject = isJsonObject(inherited) ? inherited : {};
    const localObject = isJsonObject(local) ? local : {};
    const keys = new Set([
      ...Object.keys(inheritedObject),
      ...Object.keys(localObject),
    ]);
    const merged: Record<string, JsonValue> = {};
    for (const key of keys) {
      merged[key] = mergedTokenShape(inheritedObject[key], localObject[key]);
    }
    return merged;
  }
  return local === undefined ? inherited : local;
}

function withCurrentOption(options: string[], value: string) {
  if (!value || options.includes(value)) return options;
  return [value, ...options];
}

function isFontWeightKey(key: string) {
  return /fontWeight$/i.test(key) || /Weight$/i.test(key);
}

function isFontFamilyKey(key: string, groupContext?: string) {
  return (
    /fontFamily$/i.test(key) ||
    key.endsWith(".family") ||
    (key === "family" && /font|fonts/i.test(groupContext ?? ""))
  );
}

function isFontCompanionRow(row: TokenRow, root: JsonValue) {
  const key = String(row.path[row.path.length - 1] ?? "");
  if (key !== "fontWeight" && key !== "fontStyle") return false;
  const parent = getAtPath(root, row.path.slice(0, -1));
  return (
    isJsonObject(parent) &&
    (typeof parent.fontFamily === "string" || typeof parent.family === "string")
  );
}

function fontSiblingPath(path: JsonPath, key: string) {
  return [...path.slice(0, -1), key];
}

function numericSibling(root: JsonValue, path: JsonPath, key: string): number | undefined {
  const value = getAtPath(root, [...path.slice(0, -1), key]);
  return typeof value === "number" && Number.isFinite(value) ? value : undefined;
}

export function TokenOverrideEditor({
  rootValue,
  inheritedRoot,
  hints,
  showInheritedValue = true,
  inheritedColumnLabel = "Token / inherited",
  groupContext,
  restoreMode = "remove",
  productionFontCatalog,
  paletteCatalog,
  mediaRoot,
  nativeBridge,
  onRootChange,
}: TokenOverrideEditorProps) {
  const displayRoot = mergedTokenShape(inheritedRoot, rootValue);
  const rows = rowsWithDictionaryDefaults(
    flattenPrimitiveTokens(displayRoot),
    hints,
  ).filter((row) => {
    if (isFontCompanionRow(row, displayRoot)) return false;
    const hint = hintForPath(hints, row.path, row.value, groupContext);
    return hint.field?.ui?.hidden !== true;
  });
  const [activeTokenGroup, setActiveTokenGroup] = useState("");

  function restoreValue(path: JsonPath, inheritedValue: JsonValue) {
    onRootChange(
      restoreMode === "set"
        ? setAtPath(rootValue, path, inheritedValue)
        : deleteAtPathAndPrune(rootValue, path),
    );
  }

  function setFontSelectionOverride(
    path: JsonPath,
    selection: ProductionFontSelection,
    options: { lockFamily?: boolean } = {},
  ) {
    const parentPath = path.slice(0, -1);
    let nextRoot = options.lockFamily
      ? rootValue
      : setAtPath(rootValue, path, selection.fontFamily);
    nextRoot = setAtPath(nextRoot, [...parentPath, "fontWeight"], selection.fontWeight);
    nextRoot = setAtPath(nextRoot, [...parentPath, "fontStyle"], selection.fontStyle);
    if (options.lockFamily) {
      onRootChange(nextRoot);
      return;
    }
    const productionFontId = productionFontIdForFamily(
      productionFontCatalog,
      selection.fontFamily,
    );
    if (productionFontId) {
      nextRoot = setAtPath(
        setAtPath(nextRoot, [...parentPath, "productionFontId"], productionFontId),
        [...parentPath, "source"],
        "production_font_family",
      );
    }
    onRootChange(nextRoot);
  }

  function restoreFontSelection(path: JsonPath) {
    let nextRoot =
      restoreMode === "set"
        ? setAtPath(rootValue, path, getAtPath(inheritedRoot, path))
        : deleteAtPathAndPrune(rootValue, path);
    for (const key of ["fontWeight", "fontStyle", "productionFontId", "source"]) {
      const siblingPath = fontSiblingPath(path, key);
      nextRoot =
        restoreMode === "set" && hasAtPath(inheritedRoot, siblingPath)
          ? setAtPath(nextRoot, siblingPath, getAtPath(inheritedRoot, siblingPath))
          : deleteAtPathAndPrune(nextRoot, siblingPath);
    }
    onRootChange(nextRoot);
  }

  function descriptorForRow(row: TokenRow) {
    const hasBaselineValue = hasAtPath(inheritedRoot, row.path);
    const baselineValue = hasBaselineValue
      ? getAtPath(inheritedRoot, row.path)
      : row.value;
    const hint = hintForPath(hints, row.path, baselineValue, groupContext);
    return hint.field
      ? createJsonFieldDescriptor({
          binding: {
            field: hint.field,
            outputPath: row.path.map(String),
          },
          localRoot: rootValue,
          parentRoot: inheritedRoot,
          restoreMode,
          onRootChange,
        })
      : undefined;
  }

  function restorePaths(paths: readonly JsonPath[]) {
    let nextRoot = rootValue;
    for (const path of paths) {
      const baseline = hasAtPath(inheritedRoot, path)
        ? getAtPath(inheritedRoot, path)
        : undefined;
      nextRoot =
        restoreMode === "set" && baseline !== undefined
          ? setAtPath(nextRoot, path, baseline)
          : deleteAtPathAndPrune(nextRoot, path);
    }
    onRootChange(nextRoot);
  }

  function renderDictionaryControl(
    descriptor: EditorFieldDescriptor,
    inheritedDisplayValue: string,
    descriptorSelectOptions: EditorFieldDescriptor["selectOptions"],
  ) {
    const path = descriptor.source.path ?? [];
    const imagePreview =
      descriptor.field.kind === "relativeFilePath" ||
      descriptor.field.kind === "filePath"
        ? {
            scale: numericSibling(displayRoot, [...path], "scale"),
            offsetX: numericSibling(displayRoot, [...path], "offsetX"),
            offsetY: numericSibling(displayRoot, [...path], "offsetY"),
            baseSize: numericSibling(displayRoot, [...path], "baseSize"),
          }
        : undefined;
    return (
      <DictionaryFieldControl
        {...toDictionaryFieldControlProps({
          ...descriptor,
          selectOptions: descriptorSelectOptions,
          placeholder: showInheritedValue ? inheritedDisplayValue : "",
        }, {
          fileBrowser: nativeBridge,
          mediaRoot,
          paletteCatalog,
          imagePreview,
        })}
      />
    );
  }

  function renderPairRows(pairRows: TokenRow[], groupKey?: string) {
    const descriptors = pairRows
      .map((row) => ({ row, descriptor: descriptorForRow(row) }))
      .filter(
        (entry): entry is { row: TokenRow; descriptor: EditorFieldDescriptor } =>
          Boolean(entry.descriptor),
      );
    const firstDescriptor = descriptors[0]?.descriptor;
    const pair = firstDescriptor?.field.ui?.pair;
    if (!pair || descriptors.length < 2) return null;
    const rowState = descriptors.some((entry) => entry.descriptor.state === "local")
      ? "override"
      : descriptors.some((entry) => entry.descriptor.state === "invalid")
        ? "invalid"
        : "default";
    const canRestore = descriptors.some((entry) => entry.descriptor.canRestore);
    const pairKind = descriptors.some(
      (entry) => entry.descriptor.field.kind === "paletteColorToken",
    )
      ? " record-editor-field-pair-color"
      : "";

    return (
      <InspectorFieldRow
        key={`${groupKey ?? ""}:${pair.id}`}
        className={`token-override-row ${DICTIONARY_FIELD_CLASS} ${
          rowState === "override" ? "has-override" : ""
        }`}
        state={rowState}
        label={<strong title={pair.id}>{pair.label}</strong>}
        data-field-id={pair.id}
        data-control-kind="pair"
        control={
          <div className={`record-editor-field-pair${pairKind} token-override-input dictionary-control`}>
            {descriptors.map(({ row, descriptor }) => {
              const role = descriptor.field.ui?.pair?.role ?? "";
              const inheritedValue = hasAtPath(inheritedRoot, row.path)
                ? getAtPath(inheritedRoot, row.path)
                : (descriptor.defaultValue as JsonValue | undefined) ?? row.value ?? null;
              const inheritedDisplayValue = tokenDisplayValue(
                inheritedValue,
              );
              const descriptorSelectOptions =
                descriptor.selectOptions;
              return (
                <>
                  <span
                    className="record-editor-field-pair-label"
                    key={`${pathLabel(row.path)}:label`}
                  >
                    {role}
                  </span>
                  <div
                    className="record-editor-field-pair-control"
                    key={`${pathLabel(row.path)}:control`}
                  >
                    {renderDictionaryControl(
                      descriptor,
                      inheritedDisplayValue,
                      descriptorSelectOptions,
                    )}
                  </div>
                </>
              );
            })}
          </div>
        }
        restore={
          canRestore ? (
            <InspectorRestoreButton
              label={`Restore ${pair.label}`}
              onClick={() => restorePaths(descriptors.map((entry) => entry.row.path))}
            />
          ) : undefined
        }
      />
    );
  }

  function renderRows(rowList: TokenRow[], groupKey?: string) {
    const consumedPairs = new Set<string>();
    return rowList.flatMap((row) => {
      const descriptor = descriptorForRow(row);
      const pair = descriptor?.field.ui?.pair;
      if (!pair) return [renderRow(row, groupKey)];
      if (consumedPairs.has(pair.id)) return [];
      consumedPairs.add(pair.id);
      const pairRows = rowList.filter(
        (candidate) => descriptorForRow(candidate)?.field.ui?.pair?.id === pair.id,
      );
      return [renderPairRows(pairRows, groupKey) ?? renderRow(row, groupKey)];
    });
  }

  function renderRow(row: TokenRow, groupKey?: string) {
    const hasLocalValue = hasAtPath(rootValue, row.path);
    const localValue = hasLocalValue ? getAtPath(rootValue, row.path) : null;
    const hasBaselineValue = hasAtPath(inheritedRoot, row.path);
    const baselineValue = hasBaselineValue
      ? getAtPath(inheritedRoot, row.path)
      : row.value;
    const effectiveValue = hasLocalValue ? localValue : baselineValue;
    const hasOverride =
      hasLocalValue && !deepEqualJson(localValue, baselineValue);
    const hint = hintForPath(hints, row.path, baselineValue, groupContext);
    const descriptor = descriptorForRow(row);
    const label = compactLabelForGroup(
      hint.label ?? friendlyPathLeafLabel(row.path),
      groupKey ?? groupContext,
    );
    const key = pathLabel(row.path);
    const widget = widgetForRow(
      hint,
      key,
      effectiveValue,
      groupKey ?? groupContext,
    );
    const hasFontOverride =
      widget === "font" &&
      ["fontFamily", "family", "fontWeight", "fontStyle"].some((fontKey) => {
        const candidatePath =
          fontKey === "fontFamily" || fontKey === "family"
            ? row.path
            : fontSiblingPath(row.path, fontKey);
        return (
          hasAtPath(rootValue, candidatePath) &&
          !deepEqualJson(
            getAtPath(rootValue, candidatePath),
            getAtPath(inheritedRoot, candidatePath),
          )
        );
      });
    const rowHasOverride = widget === "font" ? hasFontOverride : hasOverride;
    const stringValue = hasOverride ? tokenDisplayValue(localValue) : "";
    const inheritedDisplayValue = tokenDisplayValue(baselineValue);
    const selectOptions = withCurrentOption(
      widget === "font" || isFontWeightKey(key)
        ? []
        : hint.options ?? [],
      stringValue,
    );
    const descriptorControlKind = descriptor
      ? controlDefinitionForField(descriptor.field).control
      : undefined;
    const descriptorSelectOptions =
      descriptor && hint.options?.length
        ? {
            allowEmpty: false,
            options: withCurrentOption(
              hint.options,
              String(descriptor.displayValue ?? ""),
            ).map((option) => ({
              value: String(option),
              label: String(option),
            })),
          }
        : descriptor?.selectOptions;
    const rowState = descriptor
      ? descriptor.state === "invalid"
        ? "invalid"
        : descriptor.state === "local"
          ? "override"
          : "default"
      : rowHasOverride
        ? "override"
        : "default";

    return (
      <InspectorFieldRow
        key={key}
        className={`token-override-row ${
          descriptor ? DICTIONARY_FIELD_CLASS : ""
        } ${descriptor?.field.ui?.multiline === true ? "is-multiline" : ""} ${
          rowState === "override" ? "has-override" : ""
        }`}
        state={rowState}
        label={<strong title={key}>{label}</strong>}
        data-field-id={descriptor?.field.id}
        data-value-kind={descriptor?.field.kind}
        data-control-kind={descriptorControlKind}
        data-source-kind={descriptor?.source.kind}
        control={
          descriptor ? (
            <div className="token-override-input">
              {renderDictionaryControl(
                {
                  ...descriptor,
                  selectOptions: descriptorSelectOptions,
                },
                inheritedDisplayValue,
                descriptorSelectOptions,
              )}
            </div>
          ) : (
            <span>{inheritedColumnLabel}</span>
          )
        }
        restore={
          descriptor?.canRestore && descriptor.actions.restore ? (
            <InspectorRestoreButton
              label={`Restore ${label} to ${inheritedDisplayValue}`}
              onClick={descriptor.actions.restore}
            />
          ) : undefined
        }
      />
    );
  }

  return (
    <div className="token-override-editor">
      {groupedRows(rows, groupContext).map((group) => {
        if (!group.renderAsGroup) {
          return renderRows(group.rows, group.key || undefined);
        }
        return (
          <EditorSubsectionAccordion
            key={group.key}
            group={group.key}
            activeGroup={activeTokenGroup}
            warning={group.rows.some((row) => hasAtPath(rootValue, row.path))}
            onToggle={setActiveTokenGroup}
          >
            {renderRows(group.rows, group.key)}
          </EditorSubsectionAccordion>
        );
      })}
    </div>
  );
}
