import { Fragment, useState, type ReactNode } from "react";
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
  surfaceStyleNormalize,
} from "../../../domain/value-system/index.js";
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
  ToggleInspectorLabel,
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
  groupHeaderExtras?: Record<string, ReactNode>;
  inlineSingleGroup?: boolean;
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

const FIELD_KIND_HIGH_ROW = new Set([
  "filePath",
  "relativeFilePath",
  "surfaceStyle",
]);

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
  if (key.startsWith("[")) {
    try {
      const parsed = JSON.parse(key) as unknown;
      if (
        Array.isArray(parsed) &&
        parsed.every(
          (part) => typeof part === "string" || typeof part === "number",
        )
      ) {
        return parsed;
      }
    } catch {
      // Fall through to legacy dot path.
    }
  }
  return key.split(".").filter(Boolean);
}

function contextualHintPath(
  hint: JsonUiHint,
  key: string,
  groupContext?: string,
): JsonPath | undefined {
  const path = hint.storagePath ?? pathFromHintKey(hint.canonicalPath ?? key);
  if (!path.length) return undefined;
  if (!groupContext) return path;
  if (path[0] === groupContext) return path.slice(1);
  if (hint.group === groupContext || hint.field?.ui?.group?.id === groupContext) {
    return path;
  }
  return undefined;
}

function rowBelongsToContext(
  hint: JsonUiHint,
  path: JsonPath,
  groupContext?: string,
) {
  if (!groupContext) return true;
  if (path[0] === groupContext) return true;
  if (hint.storagePath?.[0] === groupContext) return true;
  return hint.group === groupContext || hint.field?.ui?.group?.id === groupContext;
}

function rowsWithDictionaryDefaults(
  rows: TokenRow[],
  hints: JsonUiHints,
  groupContext?: string,
): TokenRow[] {
  const byPath = new Map(rows.map((row) => [pathLabel(row.path), row]));
  for (const [key, hint] of Object.entries(hints)) {
    if (!hint.field) continue;
    const path = contextualHintPath(hint, key, groupContext);
    if (!path?.length) continue;
    if (hint.field.kind === "surfaceStyle") {
      for (const currentPathKey of Array.from(byPath.keys())) {
        const currentPath = currentPathKey.split(".");
        const isDescendant =
          currentPath.length > path.length &&
          path.every((part, index) => String(part) === currentPath[index]);
        if (isDescendant) byPath.delete(currentPathKey);
      }
      const pathKey = pathLabel(path);
      if (!byPath.has(pathKey)) {
        byPath.set(pathKey, {
          path,
          value: surfaceStyleNormalize(hint.field.defaultValue ?? {}) as JsonValue,
        });
      }
      continue;
    }
    const pathKey = pathLabel(path);
    if (byPath.has(pathKey)) continue;
    byPath.set(pathKey, {
      path,
      value: (hint.field.defaultValue ?? "") as JsonValue,
    });
  }
  const rowsWithDefaults = Array.from(byPath.values());
  const order = new Map<string, number>();
  Object.entries(hints).forEach(([key, hint], index) => {
    const path = contextualHintPath(hint, key, groupContext);
    if (path?.length) order.set(pathLabel(path), index);
  });
  return rowsWithDefaults.sort(
    (left, right) =>
      (order.get(pathLabel(left.path)) ?? Number.MAX_SAFE_INTEGER) -
      (order.get(pathLabel(right.path)) ?? Number.MAX_SAFE_INTEGER),
  );
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
  if (firstKey === "avatar") return "avatar";
  if (firstKey === "style") return "bubble";
  if (firstKey === "surfaceRelief") return "bubble";
  if (firstKey.startsWith("messageLabel")) return "label";
  if (firstKey === "avatarSize" || firstKey === "avatarGap") return "avatar";
  if (
    firstKey === "paddingX" ||
    firstKey === "paddingY" ||
    firstKey === "contentMetaGap" ||
    firstKey === "maxWidthRatio" ||
    firstKey === "radius" ||
    firstKey === "borderWidth" ||
    firstKey === "borderColorToken" ||
    firstKey === "shadowEnabled" ||
    firstKey === "surfaceReliefEnabled"
  ) {
    return "bubble";
  }
  return "general";
}

function groupedRows(
  rows: TokenRow[],
  hints: JsonUiHints,
  groupContext?: string,
): TokenRowGroup[] {
  if (groupContext === "chatBubbles") {
    const groups = new Map<string, TokenRow[]>();
    for (const row of rows) {
      const group = chatBubbleGroupForRow(row);
      groups.set(group, [...(groups.get(group) ?? []), row]);
    }
    return Array.from(groups.keys())
      .sort((left, right) =>
        friendlyGroupLabel(left).localeCompare(friendlyGroupLabel(right)),
      )
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
    const hint = hintForPath(hints, row.path, row.value, groupContext);
    const group = hint.field?.ui?.group?.id ?? "";
    groups.set(group, [...(groups.get(group) ?? []), row]);
  }
  return Array.from(groups, ([key, groupRows]) => {
    const label =
      hintForPath(hints, groupRows[0]?.path ?? [], groupRows[0]?.value ?? null, groupContext)
        .field?.ui?.group?.label ?? friendlyGroupLabel(key);
    return {
      key,
      label,
      rows: groupRows,
      renderAsGroup: Boolean(key),
    };
  }).sort((left, right) => {
    if (!left.renderAsGroup && right.renderAsGroup) return -1;
    if (left.renderAsGroup && !right.renderAsGroup) return 1;
    return left.label.localeCompare(right.label);
  });
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

export function tokenOverrideHasNonDefaultFields({
  rootValue,
  inheritedRoot,
  hints,
  groupContext,
}: {
  rootValue: JsonValue;
  inheritedRoot: JsonValue;
  hints: JsonUiHints;
  groupContext?: string;
}) {
  const displayRoot = mergedTokenShape(inheritedRoot, rootValue);
  const rows = rowsWithDictionaryDefaults(
    flattenPrimitiveTokens(displayRoot),
    hints,
    groupContext,
  ).filter((row) => {
    if (isFontCompanionRow(row, displayRoot)) return false;
    const hint = hintForPath(hints, row.path, row.value, groupContext);
    if (!hint.field) return false;
    if (!rowBelongsToContext(hint, row.path, groupContext)) return false;
    return hint.field?.ui?.hidden !== true;
  });
  const defaultsByPath = new Map(
    rows.map((row) => [pathLabel(row.path), row.value]),
  );

  return rows.some((row) => {
    const hint = hintForPath(hints, row.path, row.value, groupContext);
    const key = pathLabel(row.path);
    const effectiveValue = hasAtPath(rootValue, row.path)
      ? getAtPath(rootValue, row.path)
      : hasAtPath(inheritedRoot, row.path)
        ? getAtPath(inheritedRoot, row.path)
        : row.value;
    const widget = widgetForRow(hint, key, effectiveValue, groupContext);
    if (widget === "font") {
      return ["fontFamily", "family", "fontWeight", "fontStyle"].some(
        (fontKey) => {
          const candidatePath =
            fontKey === "fontFamily" || fontKey === "family"
              ? row.path
              : fontSiblingPath(row.path, fontKey);
          return (
            hasAtPath(rootValue, candidatePath) &&
            !deepEqualJson(
              getAtPath(rootValue, candidatePath),
              hasAtPath(inheritedRoot, candidatePath)
                ? getAtPath(inheritedRoot, candidatePath)
                : defaultsByPath.get(pathLabel(candidatePath)) ?? null,
            )
          );
        },
      );
    }
    if (!hasAtPath(rootValue, row.path)) return false;
    const localValue = getAtPath(rootValue, row.path);
    const baselineValue = hasAtPath(inheritedRoot, row.path)
      ? getAtPath(inheritedRoot, row.path)
      : row.value;
    return !deepEqualJson(localValue, baselineValue);
  });
}

export function TokenOverrideEditor({
  rootValue,
  inheritedRoot,
  hints,
  showInheritedValue = true,
  inheritedColumnLabel = "Token / inherited",
  groupContext,
  groupHeaderExtras,
  inlineSingleGroup = false,
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
    groupContext,
  ).filter((row) => {
    if (isFontCompanionRow(row, displayRoot)) return false;
    const hint = hintForPath(hints, row.path, row.value, groupContext);
    if (!hint.field) return false;
    if (!rowBelongsToContext(hint, row.path, groupContext)) return false;
    return hint.field?.ui?.hidden !== true;
  });
  const defaultsByPath = new Map(
    rows.map((row) => [pathLabel(row.path), row.value]),
  );
  const rowGroups = groupedRows(rows, hints, groupContext);
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
          productionFontCatalog,
          imagePreview,
        })}
      />
    );
  }

  function rowHasNonDefaultValue(row: TokenRow) {
    const descriptor = descriptorForRow(row);
    const key = pathLabel(row.path);
    const hint = hintForPath(hints, row.path, row.value, groupContext);
    const effectiveValue = hasAtPath(rootValue, row.path)
      ? getAtPath(rootValue, row.path)
      : hasAtPath(inheritedRoot, row.path)
        ? getAtPath(inheritedRoot, row.path)
        : row.value;
    const widget = widgetForRow(hint, key, effectiveValue, groupContext);
    if (widget === "font") {
      return ["fontFamily", "family", "fontWeight", "fontStyle"].some(
        (fontKey) => {
          const candidatePath =
            fontKey === "fontFamily" || fontKey === "family"
              ? row.path
              : fontSiblingPath(row.path, fontKey);
          return (
            hasAtPath(rootValue, candidatePath) &&
            !deepEqualJson(
              getAtPath(rootValue, candidatePath),
              hasAtPath(inheritedRoot, candidatePath)
                ? getAtPath(inheritedRoot, candidatePath)
                : defaultsByPath.get(pathLabel(candidatePath)) ?? null,
            )
          );
        },
      );
    }
    if (descriptor) return descriptor.state === "local";
    if (!hasAtPath(rootValue, row.path)) return false;
    const localValue = getAtPath(rootValue, row.path);
    const baselineValue = hasAtPath(inheritedRoot, row.path)
      ? getAtPath(inheritedRoot, row.path)
      : row.value;
    return !deepEqualJson(localValue, baselineValue);
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
          descriptors.some((entry) => FIELD_KIND_HIGH_ROW.has(entry.descriptor.field.kind))
            ? "is-multiline"
            : ""
        } ${
          rowState === "override" ? "has-override" : ""
        }`}
        state={rowState}
        label={
          <ToggleInspectorLabel
            label={<strong>{pair.label}</strong>}
            technicalLabel={pair.id}
          />
        }
        data-field-id={pair.id}
        data-control-kind="pair"
        control={
          <div className={`record-editor-field-pair${pairKind} dictionary-control`}>
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
                <Fragment key={pathLabel(row.path)}>
                  <span className="record-editor-field-pair-label">{role}</span>
                  <div className="record-editor-field-pair-control">
                    {renderDictionaryControl(
                      descriptor,
                      inheritedDisplayValue,
                      descriptorSelectOptions,
                    )}
                  </div>
                </Fragment>
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
        } ${
          descriptor?.field.ui?.multiline === true ||
          (descriptor ? FIELD_KIND_HIGH_ROW.has(descriptor.field.kind) : false)
            ? "is-multiline"
            : ""
        } ${
          rowState === "override" ? "has-override" : ""
        }`}
        state={rowState}
        label={
          <ToggleInspectorLabel
            label={<strong>{label}</strong>}
            technicalLabel={descriptor?.field.id ?? key}
          />
        }
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
      {rowGroups.map((group) => {
        if (
          !group.renderAsGroup ||
          (inlineSingleGroup && rowGroups.length === 1)
        ) {
          return renderRows(group.rows, group.key || undefined);
        }
        return (
          <EditorSubsectionAccordion
            key={group.key}
            group={group.key}
            label={group.label}
            activeGroup={activeTokenGroup}
            warning={group.rows.some(rowHasNonDefaultValue)}
            onToggle={setActiveTokenGroup}
          >
            {groupHeaderExtras?.[group.key] ?? null}
            {renderRows(group.rows, group.key)}
          </EditorSubsectionAccordion>
        );
      })}
    </div>
  );
}
