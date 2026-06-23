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

interface ModeColorEditorProps {
  rootValue: JsonValue;
  inheritedRoot?: JsonValue;
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

function groupIcon(label: string): string {
  const normalized = label.toLowerCase();
  if (normalized.includes("app")) return "▣";
  if (normalized.includes("header") || normalized.includes("navigation")) return "▤";
  if (normalized.includes("chat") || normalized.includes("bubble")) return "☰";
  if (normalized.includes("status")) return "▥";
  if (normalized.includes("cursor")) return "⌁";
  if (normalized.includes("text") || normalized.includes("typography")) return "T";
  if (normalized.includes("surface") || normalized.includes("background")) return "▧";
  return "◐";
}

function isHexColor(value: unknown): value is string {
  return typeof value === "string" && /^#[0-9a-fA-F]{6}$/.test(value);
}

function rgbaToHexAndAlpha(value: string): { hex: string; alpha: number } | null {
  const match = value
    .trim()
    .match(/^rgba\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(0|1|0?\.\d+)\s*\)$/i);
  if (!match) return null;
  const [, red, green, blue, alpha] = match;
  const channels = [red, green, blue].map((channel) =>
    Math.max(0, Math.min(255, Number(channel))),
  );
  return {
    hex: `#${channels
      .map((channel) => channel.toString(16).padStart(2, "0"))
      .join("")}`,
    alpha: Math.max(0, Math.min(1, Number(alpha))),
  };
}

function hexToRgba(hex: string, alpha: number) {
  const normalized = hex.replace("#", "");
  const red = Number.parseInt(normalized.slice(0, 2), 16);
  const green = Number.parseInt(normalized.slice(2, 4), 16);
  const blue = Number.parseInt(normalized.slice(4, 6), 16);
  return `rgba(${red},${green},${blue},${Math.max(0, Math.min(1, alpha))})`;
}

function isColorRolePath(path: JsonPath): boolean {
  const key = String(path[path.length - 1] ?? "");
  return /(color|background|text|accent|surface|separator)$/i.test(key);
}

function flattenColorPaths(value: JsonValue, path: JsonPath = []): JsonPath[] {
  if (Array.isArray(value)) {
    return value.flatMap((entry, index) =>
      flattenColorPaths(entry, [...path, index]),
    );
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

function uniqueRolePaths(rootValue: JsonValue, inheritedRoot?: JsonValue) {
  const keys = new Set<string>();
  const paths: JsonPath[] = [];
  for (const root of [rootValue, inheritedRoot]) {
    if (!root) continue;
    for (const mode of ["light", "dark"] as const) {
      for (const rolePath of flattenColorPaths(modeRoot(root, mode))) {
        const key = pathLabel(rolePath);
        if (!keys.has(key)) {
          keys.add(key);
          paths.push(rolePath);
        }
      }
    }
  }
  return paths.sort((a, b) => {
    const orderDelta = groupOrder(roleGroup(a)) - groupOrder(roleGroup(b));
    return orderDelta || pathLabel(a).localeCompare(pathLabel(b));
  });
}

function colorAt(root: JsonValue, mode: "light" | "dark", rolePath: JsonPath) {
  const value = getAtPath(root, ["modes", mode, ...rolePath]);
  return typeof value === "string" ? value : "";
}

function inheritedColorAt(
  root: JsonValue | undefined,
  mode: "light" | "dark",
  rolePath: JsonPath,
) {
  if (!root) return undefined;
  const value = colorAt(root, mode, rolePath);
  return value || undefined;
}

function roleLabel(path: JsonPath) {
  const leaf = friendlyPathLeafLabel(path);
  const parent =
    path.length > 1
      ? path.slice(0, -1).map((segment) => friendlyGroupLabel(String(segment))).join(" · ")
      : "";
  return parent ? `${parent} · ${leaf}` : leaf;
}

function roleGroup(path: JsonPath): string {
  return typeof path[0] === "string" ? path[0] : "colors";
}

function groupOrder(group: string): number {
  const normalized = group.toLowerCase();
  const order: Record<string, number> = {
    colors: 10,
    header: 20,
    chatbubbles: 30,
    bubbles: 30,
    messages: 35,
    statusbar: 40,
    cursor: 50,
  };
  return order[normalized] ?? 100;
}

function colorGroupLabel(group: string): string {
  if (group === "colors") return "App Colors";
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
  })).sort((a, b) => groupOrder(a.group) - groupOrder(b.group));
}

export function hasModeColorOverrides(
  rootValue: JsonValue,
  inheritedRoot?: JsonValue,
) {
  if (!inheritedRoot) return false;
  return uniqueRolePaths(rootValue, inheritedRoot).some((rolePath) =>
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
  onRootChange,
}: ModeColorEditorProps) {
  const colorGroups = groupedRolePaths(uniqueRolePaths(rootValue, inheritedRoot));

  function updateColor(mode: "light" | "dark", rolePath: JsonPath, raw: string) {
    if (!raw) {
      onRootChange(deleteAtPathAndPrune(rootValue, ["modes", mode, ...rolePath]));
      return;
    }
    onRootChange(
      setAtPath(
        rootValue,
        ["modes", mode, ...rolePath],
        isHexColor(raw) ? raw.toLowerCase() : raw,
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
    const inheritedValue = inheritedColorAt(inheritedRoot, mode, rolePath);
    if (!inheritedValue) return false;
    const localPath: JsonPath = ["modes", mode, ...rolePath];
    if (!hasAtPath(rootValue, localPath)) return false;
    return colorAt(rootValue, mode, rolePath) !== inheritedValue;
  }

  function hasRoleOverride(rolePath: JsonPath) {
    return hasModeOverride("light", rolePath) || hasModeOverride("dark", rolePath);
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
        <section key={group.group} className="mode-color-group">
          <h4>
            <span className="editor-group-icon" aria-hidden="true">
              {groupIcon(group.label)}
            </span>
            {group.label}
          </h4>
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
              lightValue: colorAt(rootValue, "light", rolePath),
              darkValue: colorAt(rootValue, "dark", rolePath),
              inheritedLightValue: inheritedColorAt(
                inheritedRoot,
                "light",
                rolePath,
              ),
              inheritedDarkValue: inheritedColorAt(
                inheritedRoot,
                "dark",
                rolePath,
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
                  const alphaColor =
                    typeof displayValue === "string"
                      ? rgbaToHexAndAlpha(displayValue)
                      : null;
                  const canPickColor = isHexColor(displayValue);
                  return (
                    <span
                      key={mode}
                      className="json-color-pair token-color-pair"
                    >
                      {alphaColor ? (
                        <span className="json-alpha-color-pair">
                          <input
                            aria-label={`${key} ${mode} color picker`}
                            type="color"
                            value={alphaColor.hex}
                            onChange={(event) =>
                              updateColor(
                                mode,
                                role.rolePath,
                                hexToRgba(event.target.value, alphaColor.alpha),
                              )
                            }
                          />
                          <input
                            aria-label={`${key} ${mode} alpha`}
                            className="json-value-control"
                            type="number"
                            min={0}
                            max={1}
                            step={0.01}
                            value={String(alphaColor.alpha)}
                            onChange={(event) =>
                              updateColor(
                                mode,
                                role.rolePath,
                                hexToRgba(alphaColor.hex, Number(event.target.value)),
                              )
                            }
                          />
                        </span>
                      ) : canPickColor ? (
                        <>
                          <input
                            aria-label={`${key} ${mode} color picker`}
                            type="color"
                            value={displayValue}
                            onChange={(event) =>
                              updateColor(mode, role.rolePath, event.target.value)
                            }
                          />
                        </>
                      ) : (
                        <input
                          aria-label={`${key} ${mode} value`}
                          type="text"
                          placeholder={
                            inherited ? `Inherit ${inherited}` : "Inherit"
                          }
                          value={value}
                          onChange={(event) =>
                            updateColor(mode, role.rolePath, event.target.value)
                          }
                        />
                      )}
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
        </section>
      ))}
    </div>
  );
}
