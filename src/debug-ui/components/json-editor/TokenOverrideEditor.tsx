import { useState } from "react";
import { DeferredNumberInput } from "../../editor-ui/DeferredNumberInput.js";
import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import { EditorSubsectionAccordion } from "../../editor-ui/EditorSubsectionAccordion.js";
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

function groupIcon(label: string): string {
  const normalized = label.toLowerCase();
  if (normalized.includes("typography") || normalized.includes("font")) return "T";
  if (normalized.includes("color")) return "◐";
  if (normalized.includes("header")) return "▤";
  if (normalized.includes("message") || normalized.includes("bubble")) return "☰";
  if (normalized.includes("spacing") || normalized.includes("padding")) return "↔";
  if (normalized.includes("radius") || normalized.includes("radii")) return "◜";
  if (normalized.includes("shadow") || normalized.includes("elevation")) return "◒";
  if (normalized.includes("status")) return "▥";
  if (normalized.includes("cursor")) return "⌁";
  if (normalized.includes("wallpaper") || normalized.includes("background")) return "▧";
  return "◇";
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
  onRootChange,
}: TokenOverrideEditorProps) {
  const displayRoot = mergedTokenShape(inheritedRoot, rootValue);
  const rows = flattenPrimitiveTokens(displayRoot).filter(
    (row) => !isFontCompanionRow(row, displayRoot),
  );
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

    return (
      <InspectorFieldRow
        key={key}
        className={`token-override-row ${rowHasOverride ? "has-override" : ""}`}
        state={rowHasOverride ? "override" : "default"}
        label={<strong title={key}>{label}</strong>}
        control={
          <div className="token-override-input">
          {widget === "checkbox" ? (
            <select
              aria-label={`${label} override`}
              className={!rowHasOverride ? "is-inherited-value" : undefined}
              value={rowHasOverride ? String(Boolean(localValue)) : ""}
              onChange={(event) => {
                const raw = event.target.value;
                onRootChange(
                  raw === ""
                    ? restoreMode === "set"
                      ? setAtPath(rootValue, row.path, baselineValue)
                      : deleteAtPathAndPrune(rootValue, row.path)
                    : setAtPath(rootValue, row.path, raw === "true"),
                );
              }}
            >
              <option value="">{inheritedDisplayValue}</option>
              <option value="true">true</option>
              <option value="false">false</option>
            </select>
          ) : widget === "font" ? (
            <ProductionFontSelector
              compact
              catalog={productionFontCatalog}
              inherited={!rowHasOverride}
              lockFamily={hint.lockFontFamily}
              value={{
                fontFamily: effectiveValue,
                fontWeight: getAtPath(displayRoot, [...row.path.slice(0, -1), "fontWeight"]),
                fontStyle: getAtPath(displayRoot, [...row.path.slice(0, -1), "fontStyle"]),
              }}
              onChange={(nextFont) =>
                setFontSelectionOverride(row.path, nextFont, {
                  lockFamily: hint.lockFontFamily,
                })
              }
            />
          ) : widget === "select" ? (
            <select
              aria-label={`${label} override`}
              className={!rowHasOverride ? "is-inherited-value" : undefined}
              value={stringValue}
              onChange={(event) => {
                const raw = event.target.value;
                if (raw === "") {
                  restoreValue(row.path, baselineValue);
                  return;
                }
                onRootChange(setAtPath(rootValue, row.path, raw));
              }}
            >
              <option value="">{inheritedDisplayValue}</option>
              {selectOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          ) : widget === "color" && typeof baselineValue === "string" ? (
            <span className="json-color-pair token-color-pair">
              <ColorValueEditor
                label={`${label} override`}
                value={stringValue || baselineValue}
                alpha={isRgbaColor(String(stringValue || baselineValue))}
                paletteCatalog={paletteCatalog}
                onChange={(nextColor) =>
                  onRootChange(setAtPath(rootValue, row.path, nextColor))
                }
              />
            </span>
          ) : widget === "number" ? (
            <DeferredNumberInput
              ariaLabel={`${label} override`}
              max={hint.max}
              min={hint.min}
              placeholder={showInheritedValue ? inheritedDisplayValue : ""}
              step={hint.step ?? "any"}
              value={
                hasOverride && typeof localValue === "number"
                  ? localValue
                  : ""
              }
              onEmptyCommit={() => restoreValue(row.path, baselineValue)}
              onCommit={(nextValue) =>
                onRootChange(setAtPath(rootValue, row.path, nextValue))
              }
            />
          ) : (
            <DeferredTextInput
              ariaLabel={`${label} override`}
              placeholder={showInheritedValue ? inheritedDisplayValue : ""}
              value={stringValue}
              onCommit={(raw) => {
                if (raw === "") {
                  restoreValue(row.path, baselineValue);
                  return;
                }
                const nextValue = parseOverride(raw, baselineValue);
                if (nextValue === null) return;
                onRootChange(setAtPath(rootValue, row.path, nextValue));
              }}
            />
          )}
          </div>
        }
        restore={
          rowHasOverride ? (
            <InspectorRestoreButton
              label={`Restore ${label} to ${inheritedDisplayValue}`}
              onClick={() =>
                widget === "font"
                  ? restoreFontSelection(row.path)
                  : restoreValue(row.path, baselineValue)
              }
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
          return group.rows.map((row) => renderRow(row, group.key || undefined));
        }
        if (groupContext === "chatBubbles") {
          return (
            <EditorSubsectionAccordion
              key={group.key}
              group={group.key}
              activeGroup={activeTokenGroup}
              warning={group.rows.some((row) => hasAtPath(rootValue, row.path))}
              onToggle={setActiveTokenGroup}
            >
              {group.rows.map((row) => renderRow(row, group.key))}
            </EditorSubsectionAccordion>
          );
        }
        return (
          <section key={group.key} className="token-override-group">
            <h4>
              <span className="editor-group-icon ui-glyph" aria-hidden="true">
                {groupIcon(group.label)}
              </span>
              {group.label}
            </h4>
            {group.rows.map((row) => renderRow(row, group.key))}
          </section>
        );
      })}
    </div>
  );
}
