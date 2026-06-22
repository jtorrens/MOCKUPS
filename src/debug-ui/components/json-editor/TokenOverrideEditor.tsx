import { fontStylesForFamily, useSystemFontCatalog } from "./systemFonts.js";
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
      flattenPrimitiveTokens(entry, [...path, key]),
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
  return /^#[0-9a-fA-F]{6}$/.test(value);
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
  if (typeof value === "string" && isHexColor(value)) return "color";
  return "text";
}

function groupedRows(rows: TokenRow[]): TokenRowGroup[] {
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
  onRootChange,
}: TokenOverrideEditorProps) {
  const rows = flattenPrimitiveTokens(inheritedRoot);
  const { families, stylesByFamily } = useSystemFontCatalog();

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
    const options = fontStylesForFamily(stylesByFamily, family);
    const fallback = options[0] ?? "Regular";
    let nextRoot = setAtPath(rootValue, path, family);
    for (const [key, value] of Object.entries(sourceParent)) {
      if (!isFontWeightKey(key)) continue;
      if (typeof value === "string" && options.includes(value)) continue;
      nextRoot = setAtPath(nextRoot, [...parentPath, key], fallback);
    }
    onRootChange(nextRoot);
  }

  function renderRow(row: TokenRow, groupKey?: string) {
    const hasLocalValue = hasAtPath(rootValue, row.path);
    const overrideValue = hasLocalValue ? getAtPath(rootValue, row.path) : null;
    const hasOverride =
      hasLocalValue && !deepEqualJson(overrideValue, row.value);
    const hint = hintForPath(hints, row.path, row.value);
    const label = compactLabelForGroup(
      hint.label ?? friendlyPathLeafLabel(row.path),
      groupKey ?? groupContext,
    );
    const key = pathLabel(row.path);
    const widget = widgetForRow(hint, key, row.value, groupKey ?? groupContext);
    const stringValue = hasOverride ? tokenDisplayValue(overrideValue) : "";
    const selectOptions = withCurrentOption(
      widget === "font"
          ? families
        : isFontWeightKey(key)
          ? fontStylesForFamily(stylesByFamily, fontFamilyForWeight(row.path))
          : hint.options ?? [],
      stringValue,
    );

    return (
      <div
        key={key}
        className={`token-override-row ${hasOverride ? "has-override" : ""}`}
      >
        <strong>{label}</strong>
        <code title={key}>
          {key}
          {showInheritedValue ? (
            <small>{tokenDisplayValue(row.value)}</small>
          ) : null}
        </code>
        <div className="token-override-input">
          {widget === "checkbox" ? (
            <select
              aria-label={`${label} override`}
              value={hasOverride ? String(Boolean(overrideValue)) : ""}
              onChange={(event) => {
                const raw = event.target.value;
                onRootChange(
                  raw === ""
                    ? restoreMode === "set"
                      ? setAtPath(rootValue, row.path, row.value)
                      : deleteAtPathAndPrune(rootValue, row.path)
                    : setAtPath(rootValue, row.path, raw === "true"),
                );
              }}
            >
              <option value="">Inherit</option>
              <option value="true">true</option>
              <option value="false">false</option>
            </select>
          ) : widget === "select" || widget === "font" ? (
            <select
              aria-label={`${label} override`}
              value={stringValue}
              onChange={(event) => {
                const raw = event.target.value;
                if (raw === "") {
                  restoreValue(row.path, row.value);
                  return;
                }
                if (widget === "font" && isFontFamilyKey(key, groupKey ?? groupContext)) {
                  setFontFamilyOverride(row.path, raw);
                  return;
                }
                onRootChange(setAtPath(rootValue, row.path, raw));
              }}
            >
              <option value="">Inherit</option>
              {selectOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          ) : widget === "color" && typeof row.value === "string" ? (
            <span className="json-color-pair token-color-pair">
              <input
                aria-label={`${label} color picker`}
                type="color"
                value={
                  isHexColor(stringValue)
                    ? stringValue
                    : isHexColor(row.value)
                      ? row.value
                      : "#000000"
                }
                onChange={(event) =>
                  onRootChange(setAtPath(rootValue, row.path, event.target.value))
                }
              />
              <input
                aria-label={`${label} override`}
                type="text"
                placeholder="Inherit"
                value={stringValue}
                onChange={(event) => {
                  const raw = event.target.value;
                  if (raw === "") {
                    restoreValue(row.path, row.value);
                    return;
                  }
                  onRootChange(
                    setAtPath(
                      rootValue,
                      row.path,
                      isHexColor(raw) ? raw.toLowerCase() : raw,
                    ),
                  );
                }}
              />
            </span>
          ) : (
            <input
              aria-label={`${label} override`}
              type={widget === "number" ? "number" : "text"}
              min={hint.min}
              max={hint.max}
              step={hint.step ?? (widget === "number" ? "any" : undefined)}
              placeholder="Inherit"
              value={stringValue}
              onChange={(event) => {
                const raw = event.target.value;
                if (raw === "") {
                  restoreValue(row.path, row.value);
                  return;
                }
                const nextValue = parseOverride(raw, row.value);
                if (nextValue === null) return;
                onRootChange(setAtPath(rootValue, row.path, nextValue));
              }}
            />
          )}
          {hasOverride ? (
            <button
              type="button"
              className="json-restore-button"
              onClick={() => restoreValue(row.path, row.value)}
            >
              Restore
            </button>
          ) : null}
        </div>
      </div>
    );
  }

  return (
    <div className="token-override-editor">
      {groupedRows(rows).map((group) =>
        group.renderAsGroup ? (
          <section key={group.key} className="token-override-group">
            <h4>
              <span className="editor-group-icon" aria-hidden="true">
                {groupIcon(group.label)}
              </span>
              {group.label}
            </h4>
            {group.rows.map((row) => renderRow(row, group.key))}
          </section>
        ) : (
          group.rows.map((row) => renderRow(row, group.key || undefined))
        ),
      )}
    </div>
  );
}
