import {
  cloneJson,
  isJsonObject,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";

function defaultTokenGroupValue(groupKey: string): JsonValue {
  return groupKey === "messages" || groupKey === "participants" ? [] : {};
}

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
  delete root.keyboard;
  delete root.cursor;
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
    delete modeRoot.keyboard;
    delete modeRoot.cursor;
    const colors = isJsonObject(modeRoot.colors) ? modeRoot.colors : undefined;
    if (colors) {
      delete colors.navigationBackground;
    }
  }
  return root;
}

export function stripModuleSystemOwnedTokens(
  value: unknown,
): Record<string, JsonValue> {
  const source = isJsonObject(value as JsonValue)
    ? cloneJson(value as JsonValue)
    : ({} as JsonValue);
  const root = isJsonObject(source) ? source : {};
  delete root.cursor;
  const modes = isJsonObject(root.modes) ? root.modes : {};
  for (const mode of ["light", "dark"] as const) {
    const modeRoot = isJsonObject(modes[mode]) ? modes[mode] : undefined;
    if (!modeRoot) continue;
    delete modeRoot.cursor;
  }
  return root;
}

export function editorValueForThemeTokenGroup(
  themeTokenRoot: Record<string, JsonValue>,
  groupKey: string,
): JsonValue {
  const value = themeTokenRoot[groupKey] ?? defaultTokenGroupValue(groupKey);
  if (!isJsonObject(value)) return value;
  if (groupKey === "fonts") {
    const { source: _source, ...visibleValue } = value;
    return visibleValue;
  }
  if (groupKey === "notifications") {
    const {
      background: _background,
      titleColor: _titleColor,
      bodyColor: _bodyColor,
      ...visibleValue
    } = value;
    return visibleValue;
  }
  return value;
}

export function visibleTokenGroupValue(
  value: unknown,
  groupKey: string,
): JsonValue {
  if (!isJsonObject(value as JsonValue)) {
    return (value ?? defaultTokenGroupValue(groupKey)) as JsonValue;
  }
  const root = value as Record<string, JsonValue>;
  if (groupKey === "notifications") {
    const {
      background: _background,
      titleColor: _titleColor,
      bodyColor: _bodyColor,
      ...visibleValue
    } = root;
    return visibleValue;
  }
  const { source: _source, ...visibleValue } = root;
  return visibleValue;
}

export function editorValueForTokenGroup(
  tokenRoot: Record<string, unknown>,
  groupKey: string,
): JsonValue {
  const value = tokenRoot[groupKey] ?? defaultTokenGroupValue(groupKey);
  return visibleTokenGroupValue(value, groupKey);
}

export function inheritedValueForTokenGroup(
  tokenRoot: unknown,
  groupKey: string,
): Record<string, unknown> | null {
  if (!isJsonObject(tokenRoot as JsonValue)) return null;
  const value = (tokenRoot as Record<string, JsonValue>)[groupKey];
  if (!isJsonObject(value)) return null;
  const visibleValue = visibleTokenGroupValue(value, groupKey);
  return isJsonObject(visibleValue) ? visibleValue : null;
}

export function mergeTokenGroupWithInternalFields(
  originalValue: unknown,
  nextVisibleValue: JsonValue,
): JsonValue {
  const original = isJsonObject(originalValue as JsonValue)
    ? (originalValue as Record<string, JsonValue>)
    : {};
  const nextVisible = isJsonObject(nextVisibleValue)
    ? (nextVisibleValue as Record<string, JsonValue>)
    : {};
  const internalFields: Record<string, JsonValue> = {};
  if (Object.hasOwn(original, "source")) {
    internalFields.source = original.source;
  }
  return {
    ...internalFields,
    ...nextVisible,
  };
}

export function nextThemeTokenGroupValue({
  themeTokenRoot,
  groupKey,
  parsedValue,
}: {
  themeTokenRoot: Record<string, JsonValue>;
  groupKey: string;
  parsedValue: JsonValue;
}) {
  const originalValue = themeTokenRoot[groupKey];
  return groupKey === "fonts" && isJsonObject(parsedValue)
    ? {
        ...(isJsonObject(originalValue) ? originalValue : {}),
        ...parsedValue,
        source:
          isJsonObject(originalValue) && typeof originalValue.source === "string"
            ? originalValue.source
            : "installed_system_font",
      }
    : parsedValue;
}
