import {
  cloneJson,
  isJsonObject,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";

export function tokenEditorGroups(
  root: Record<string, unknown>,
  inheritedRoot?: unknown,
) {
  const inherited = isJsonObject(inheritedRoot as JsonValue)
    ? (inheritedRoot as Record<string, unknown>)
    : {};
  return Array.from(
    new Set([...Object.keys(root), ...Object.keys(inherited)]),
  ).filter((group) => {
    const value = root[group] ?? inherited[group];
    return (
      group !== "modes" &&
      group !== "colors" &&
      value !== null &&
      typeof value === "object" &&
      !Array.isArray(value)
    );
  });
}

export function stripAppStatusAndNavigationTokens(
  value: unknown,
): Record<string, JsonValue> {
  const source = isJsonObject(value as JsonValue)
    ? cloneJson(value as JsonValue)
    : ({} as JsonValue);
  const root = isJsonObject(source) ? source : {};
  delete root.statusBar;
  delete root.navigationBar;
  delete root.shadows;
  if (isJsonObject(root.notifications)) {
    delete root.notifications.background;
    delete root.notifications.titleColor;
    delete root.notifications.bodyColor;
  }
  const modes = isJsonObject(root.modes) ? root.modes : {};
  for (const mode of ["light", "dark"] as const) {
    const modeRoot = isJsonObject(modes[mode]) ? modes[mode] : undefined;
    if (!modeRoot) continue;
    delete modeRoot.statusBar;
    delete modeRoot.navigationBar;
    const colors = isJsonObject(modeRoot.colors) ? modeRoot.colors : undefined;
    if (colors) {
      delete colors.navigationBackground;
    }
  }
  return root;
}
