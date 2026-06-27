import { useState } from "react";
import { useSystemFontCatalog } from "./systemFonts.js";
import { DeferredNumberInput } from "../../editor-ui/DeferredNumberInput.js";
import { DeferredTextInput } from "../../editor-ui/DeferredTextInput.js";
import { EditorSubsectionAccordion } from "../../editor-ui/EditorSubsectionAccordion.js";
import { ColorValueEditor } from "./ColorValueEditor.js";
import {
  fontStylesForFamily,
  productionFontIdForFamily,
  type ProductionFontCatalog,
} from "./productionFonts.js";
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
  const rows = flattenPrimitiveTokens(displayRoot);
  const { families, stylesByFamily } = useSystemFontCatalog();
  const approvedFamilies = productionFontCatalog?.families ?? [];
  const fontFamilies = productionFontCatalog ? approvedFamilies : families;
  const [activeTokenGroup, setActiveTokenGroup] = useState("");

  function restoreValue(path: JsonPath, inheritedValue: JsonValue) {
    onRootChange(
      restoreMode === "set"
        ? setAtPath(rootValue, path, inheritedValue)
        : deleteAtPathAndPrune(rootValue, path),
    );
  }

  function fontFamilyForWeight(path: JsonPath): string | undefined {
    const parentPath = path.slice(0, -1);
    for (const familyKey of ["fontFamily", "family"]) {
      const fontFamilyPath = [...parentPath, familyKey];
      const localFamily = getAtPath(rootValue, fontFamilyPath);
      if (typeof localFamily === "string" && localFamily) return localFamily;
      const inheritedFamily = getAtPath(inheritedRoot, fontFamilyPath);
      if (typeof inheritedFamily === "string" && inheritedFamily) {
        return inheritedFamily;
      }
    }
    return undefined;
  }

  function setFontFamilyOverride(path: JsonPath, family: string) {
    const parentPath = path.slice(0, -1);
    const inheritedParent = getAtPath(inheritedRoot, parentPath);
    const localParent = getAtPath(rootValue, parentPath);
    const sourceParent = isJsonObject(localParent)
      ? localParent
      : isJsonObject(inheritedParent)
        ? inheritedParent
        : {};
    const options = fontStylesForFamily(
      productionFontCatalog,
      stylesByFamily,
      family,
    );
    const fallback = options[0] ?? "Regular";
    let nextRoot = setAtPath(rootValue, path, family);
    const productionFontId = productionFontIdForFamily(
      productionFontCatalog,
      family,
    );
    if (productionFontId) {
      nextRoot = setAtPath(
        setAtPath(nextRoot, [...parentPath, "productionFontId"], productionFontId),
        [...parentPath, "source"],
        "production_font_family",
      );
    }
    for (const [key, value] of Object.entries(sourceParent)) {
      if (!isFontWeightKey(key)) continue;
      if (typeof value === "string" && options.includes(value)) continue;
      nextRoot = setAtPath(nextRoot, [...parentPath, key], fallback);
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
    const stringValue = hasOverride ? tokenDisplayValue(localValue) : "";
    const inheritedDisplayValue = tokenDisplayValue(baselineValue);
    const selectOptions = withCurrentOption(
      widget === "font"
          ? fontFamilies
        : isFontWeightKey(key)
          ? fontStylesForFamily(
              productionFontCatalog,
              stylesByFamily,
              fontFamilyForWeight(row.path),
            )
          : hint.options ?? [],
      stringValue,
    );

    return (
      <InspectorFieldRow
        key={key}
        className={`token-override-row ${hasOverride ? "has-override" : ""}`}
        state={hasOverride ? "override" : "default"}
        label={<strong title={key}>{label}</strong>}
        control={
          <div className="token-override-input">
          {widget === "checkbox" ? (
            <select
              aria-label={`${label} override`}
              className={!hasOverride ? "is-inherited-value" : undefined}
              value={hasOverride ? String(Boolean(localValue)) : ""}
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
          ) : widget === "select" || widget === "font" ? (
            <select
              aria-label={`${label} override`}
              className={!hasOverride ? "is-inherited-value" : undefined}
              value={stringValue}
              onChange={(event) => {
                const raw = event.target.value;
                if (raw === "") {
                  restoreValue(row.path, baselineValue);
                  return;
                }
                if (widget === "font" && isFontFamilyKey(key, groupKey ?? groupContext)) {
                  setFontFamilyOverride(row.path, raw);
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
          hasOverride ? (
            <InspectorRestoreButton
              label={`Restore ${label} to ${inheritedDisplayValue}`}
              onClick={() => restoreValue(row.path, baselineValue)}
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
