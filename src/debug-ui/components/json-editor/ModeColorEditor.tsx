import { useState } from "react";
import {
  deleteAtPathAndPrune,
  getAtPath,
  hasAtPath,
  isJsonObject,
  pathLabel,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "./jsonEditorUtils.js";
import {
  compactLabelForGroup,
  friendlyGroupLabel,
  friendlyPathLeafLabel,
} from "./labels.js";
import {
  InspectorFieldRow,
  InspectorRestoreButton,
} from "../inspector/InspectorFieldRow.js";
import { ColorValueEditor } from "./ColorValueEditor.js";
import type { PaletteColorCatalog } from "./paletteColors.js";
import { EditorSubsectionAccordion } from "../../editor-ui/EditorSubsectionAccordion.js";

interface ModeColorEditorProps {
  rootValue: JsonValue;
  inheritedRoot?: JsonValue;
  hiddenGroups?: string[];
  hiddenRolePaths?: string[];
  paletteCatalog?: PaletteColorCatalog;
  onRootChange: (nextValue: JsonValue) => void;
}

interface ColorRole {
  rolePath: JsonPath;
  lightPath: JsonPath;
  darkPath: JsonPath;
  lightValue: string;
  darkValue: string;
  inheritedLightValue?: string;
  inheritedDarkValue?: string;
}

function isHexColor(value: unknown): boolean {
  return typeof value === "string" && /^#[0-9a-fA-F]{6}$/.test(value.trim());
}

function isColorRolePath(path: JsonPath): boolean {
  const key = String(path[path.length - 1] ?? "");
  if (
    path[0] === "colors" &&
    (key.startsWith("icons.") || key.startsWith("borders."))
  ) {
    return true;
  }
  return /(color|background|text|accent|surface|separator)$/i.test(key);
}

function isAlphaColorRolePath(path: JsonPath): boolean {
  const key = String(path[path.length - 1] ?? "");
  const context = path.map((segment) => String(segment)).join(".");
  return (
    (key === "background" &&
      /(notification|notifications|statusbar|statusBar|navigationbar|navigationBar)/i.test(
        context,
      )) ||
    (key === "color" && /(shadow|shadows)/i.test(context))
  );
}

function clampAlpha(value: unknown) {
  return typeof value === "number" && Number.isFinite(value)
    ? Math.max(0, Math.min(1, value))
    : 1;
}

function hexToRgba(hex: string, alpha: number) {
  const normalized = hex.replace("#", "");
  return `rgba(${Number.parseInt(normalized.slice(0, 2), 16)},${Number.parseInt(
    normalized.slice(2, 4),
    16,
  )},${Number.parseInt(normalized.slice(4, 6), 16)},${clampAlpha(alpha)})`;
}

function isPaletteAlphaColor(value: JsonValue): value is { color: string; alpha: number } {
  return (
    isJsonObject(value) &&
    typeof value.color === "string" &&
    typeof value.alpha === "number"
  );
}

function rgbaToPaletteAlphaValue(
  value: string,
  paletteCatalog?: PaletteColorCatalog,
): JsonValue {
  const match =
    /^rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(0|1|0?\.\d+)\s*\)$/i.exec(
      value.trim(),
    );
  if (!match) return value;
  const [, red, green, blue, alpha] = match;
  const hex = `#${[red, green, blue]
    .map((channel) =>
      Math.max(0, Math.min(255, Number(channel)))
        .toString(16)
        .padStart(2, "0"),
    )
    .join("")}`.toUpperCase();
  return {
    color: paletteCatalog?.byHex.get(hex)?.token ?? value,
    alpha: clampAlpha(Number(alpha)),
  };
}

function flattenColorPaths(value: JsonValue, path: JsonPath = []): JsonPath[] {
  if (Array.isArray(value)) {
    return value.flatMap((entry, index) =>
      flattenColorPaths(entry, [...path, index]),
    );
  }
  if (isPaletteAlphaColor(value)) {
    return [path];
  }
  if (isJsonObject(value)) {
    return Object.entries(value).flatMap(([key, entry]) =>
      flattenColorPaths(entry, [...path, key]),
    );
  }
  return typeof value === "string" && (isHexColor(value) || isColorRolePath(path))
    ? [path]
    : [];
}

function modeRoot(root: JsonValue, mode: "light" | "dark"): JsonValue {
  const candidate = getAtPath(root, ["modes", mode]);
  return isJsonObject(candidate) ? candidate : {};
}

function uniqueRolePaths(
  rootValue: JsonValue,
  inheritedRoot?: JsonValue,
  hiddenGroups: string[] = [],
  hiddenRolePaths: string[] = [],
) {
  const hidden = new Set(hiddenGroups.map((group) => group.toLowerCase()));
  const hiddenPaths = new Set(
    hiddenRolePaths.map((path) => path.toLowerCase()),
  );
  const keys = new Set<string>();
  const paths: JsonPath[] = [];
  for (const root of [rootValue, inheritedRoot]) {
    if (!root) continue;
    for (const mode of ["light", "dark"] as const) {
      for (const rolePath of flattenColorPaths(modeRoot(root, mode))) {
        const group = roleGroup(rolePath).toLowerCase();
        if (hidden.has(group)) continue;
        const key = pathLabel(rolePath);
        if (hiddenPaths.has(key.toLowerCase())) continue;
        if (!keys.has(key)) {
          keys.add(key);
          paths.push(rolePath);
        }
      }
    }
  }
  return paths.sort((a, b) => {
    const groupDelta = colorGroupLabel(roleGroup(a)).localeCompare(
      colorGroupLabel(roleGroup(b)),
    );
    return groupDelta || pathLabel(a).localeCompare(pathLabel(b));
  });
}

function colorAt(
  root: JsonValue,
  mode: "light" | "dark",
  rolePath: JsonPath,
  paletteCatalog?: PaletteColorCatalog,
) {
  const value = getAtPath(root, ["modes", mode, ...rolePath]);
  if (isPaletteAlphaColor(value)) {
    const paletteHex = paletteCatalog?.byToken.get(value.color)?.valueHex;
    return paletteHex ? hexToRgba(paletteHex, value.alpha) : value.color;
  }
  return typeof value === "string" ? value : "";
}

function inheritedColorAt(
  root: JsonValue | undefined,
  mode: "light" | "dark",
  rolePath: JsonPath,
  paletteCatalog?: PaletteColorCatalog,
) {
  if (!root) return undefined;
  const value = colorAt(root, mode, rolePath, paletteCatalog);
  return value || undefined;
}

function roleLabel(path: JsonPath) {
  const leafKey = String(path[path.length - 1] ?? "");
  if (
    path[0] === "colors" &&
    (leafKey.startsWith("icons.") || leafKey.startsWith("borders."))
  ) {
    return friendlyPathLeafLabel([leafKey.split(".").slice(1).join(".")]);
  }
  const leaf = friendlyPathLeafLabel(path);
  const parent =
    path.length > 1
      ? path.slice(0, -1).map((segment) => friendlyGroupLabel(String(segment))).join(" · ")
      : "";
  return parent ? `${parent} · ${leaf}` : leaf;
}

function roleGroup(path: JsonPath): string {
  const leafKey = String(path[path.length - 1] ?? "");
  if (path[0] === "colors" && leafKey.startsWith("icons.")) return "icons";
  if (path[0] === "colors" && leafKey.startsWith("borders.")) return "borders";
  return typeof path[0] === "string" ? path[0] : "colors";
}

function colorGroupLabel(group: string): string {
  if (group === "colors") return "App Colors";
  if (group === "icons") return "Icon Colors";
  if (group === "borders") return "Border Colors";
  return friendlyGroupLabel(group);
}

function groupedRolePaths(paths: JsonPath[]) {
  const groups = new Map<string, JsonPath[]>();
  for (const path of paths) {
    const group = roleGroup(path);
    groups.set(group, [...(groups.get(group) ?? []), path]);
  }
  return Array.from(groups, ([group, groupPaths]) => ({
    group,
    label: colorGroupLabel(group),
    paths: groupPaths,
  })).sort((a, b) => a.label.localeCompare(b.label));
}

export function hasModeColorOverrides(
  rootValue: JsonValue,
  inheritedRoot?: JsonValue,
  hiddenGroups: string[] = [],
  hiddenRolePaths: string[] = [],
) {
  if (!inheritedRoot) return false;
  return uniqueRolePaths(
    rootValue,
    inheritedRoot,
    hiddenGroups,
    hiddenRolePaths,
  ).some((rolePath) =>
      (["light", "dark"] as const).some((mode) => {
        const inheritedValue = inheritedColorAt(inheritedRoot, mode, rolePath);
        if (!inheritedValue) return false;
        const localPath: JsonPath = ["modes", mode, ...rolePath];
        if (!hasAtPath(rootValue, localPath)) return false;
        return colorAt(rootValue, mode, rolePath) !== inheritedValue;
      }),
  );
}

export function ModeColorEditor({
  rootValue,
  inheritedRoot,
  hiddenGroups = [],
  hiddenRolePaths = [],
  paletteCatalog,
  onRootChange,
}: ModeColorEditorProps) {
  const [activeGroup, setActiveGroup] = useState("");
  const colorGroups = groupedRolePaths(
    uniqueRolePaths(rootValue, inheritedRoot, hiddenGroups, hiddenRolePaths),
  );

  function updateColor(mode: "light" | "dark", rolePath: JsonPath, raw: string) {
    if (!raw) {
      onRootChange(deleteAtPathAndPrune(rootValue, ["modes", mode, ...rolePath]));
      return;
    }
    const isAlphaColor =
      raw.trim().toLowerCase().startsWith("rgba(") ||
      isAlphaColorRolePath(rolePath);
    onRootChange(
      setAtPath(
        rootValue,
        ["modes", mode, ...rolePath],
        isAlphaColor
          ? rgbaToPaletteAlphaValue(raw, paletteCatalog)
          : isHexColor(raw)
            ? raw.toLowerCase()
            : raw,
      ),
    );
  }

  function restoreRole(rolePath: JsonPath) {
    onRootChange(
      deleteAtPathAndPrune(
        deleteAtPathAndPrune(rootValue, ["modes", "light", ...rolePath]),
        ["modes", "dark", ...rolePath],
      ),
    );
  }

  function hasModeOverride(mode: "light" | "dark", rolePath: JsonPath) {
    if (!inheritedRoot) return false;
    const inheritedValue = inheritedColorAt(
      inheritedRoot,
      mode,
      rolePath,
      paletteCatalog,
    );
    if (!inheritedValue) return false;
    const localPath: JsonPath = ["modes", mode, ...rolePath];
    if (!hasAtPath(rootValue, localPath)) return false;
    return colorAt(rootValue, mode, rolePath, paletteCatalog) !== inheritedValue;
  }

  function hasRoleOverride(rolePath: JsonPath) {
    return hasModeOverride("light", rolePath) || hasModeOverride("dark", rolePath);
  }

  function hasGroupOverride(paths: JsonPath[]) {
    return paths.some(hasRoleOverride);
  }

  if (colorGroups.length === 0) {
    return (
      <div className="empty-record-list compact-empty">
        No mode-aware color roles yet.
      </div>
    );
  }

  return (
    <div className="mode-color-editor">
      {colorGroups.map((group) => (
        <EditorSubsectionAccordion
          key={group.group}
          group={group.label}
          activeGroup={activeGroup}
          warning={hasGroupOverride(group.paths)}
          onToggle={setActiveGroup}
        >
          <div className="mode-color-header-row" aria-hidden="true">
            <span />
            <span />
            <span className="mode-color-header-modes">
              <strong>Light</strong>
              <strong>Dark</strong>
            </span>
            <span />
          </div>
          {group.paths.map((rolePath) => {
            const key = pathLabel(rolePath);
            const hasOverride = hasRoleOverride(rolePath);
            const role: ColorRole = {
              rolePath,
              lightPath: ["modes", "light", ...rolePath],
              darkPath: ["modes", "dark", ...rolePath],
              lightValue: colorAt(rootValue, "light", rolePath, paletteCatalog),
              darkValue: colorAt(rootValue, "dark", rolePath, paletteCatalog),
              inheritedLightValue: inheritedColorAt(
                inheritedRoot,
                "light",
                rolePath,
                paletteCatalog,
              ),
              inheritedDarkValue: inheritedColorAt(
                inheritedRoot,
                "dark",
                rolePath,
                paletteCatalog,
              ),
            };
            return (
              <InspectorFieldRow
                key={key}
                className={`mode-color-row ${hasOverride ? "has-override" : ""}`}
                state={hasOverride ? "override" : "default"}
                label={
                  <strong>
                    {compactLabelForGroup(roleLabel(role.rolePath), group.group)}
                  </strong>
                }
                meta={<code title={key}>{key}</code>}
                control={
                  <div className="mode-color-controls">
                    {(["light", "dark"] as const).map((mode) => {
                      const value =
                        mode === "light" ? role.lightValue : role.darkValue;
                      const inherited =
                        mode === "light"
                          ? role.inheritedLightValue
                          : role.inheritedDarkValue;
                      const displayValue = value || inherited || "";
                      const isRgbaColor = displayValue
                        .trim()
                        .toLowerCase()
                        .startsWith("rgba(");
                      const isAlphaColor =
                        isRgbaColor || isAlphaColorRolePath(role.rolePath);
                      return (
                        <span
                          key={mode}
                          className="json-color-pair token-color-pair"
                        >
                          <ColorValueEditor
                            value={displayValue || "#000000"}
                            alpha={isAlphaColor}
                            label={`${key} ${mode}`}
                            paletteCatalog={paletteCatalog}
                            onChange={(nextValue) =>
                              updateColor(mode, role.rolePath, nextValue)
                            }
                          />
                        </span>
                      );
                    })}
                  </div>
                }
                restore={
                  hasOverride ? (
                    <InspectorRestoreButton
                      label={`Restore ${key}`}
                      onClick={() => restoreRole(role.rolePath)}
                    />
                  ) : undefined
                }
              />
            );
          })}
        </EditorSubsectionAccordion>
      ))}
    </div>
  );
}
