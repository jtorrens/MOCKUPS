import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import {
  isJsonObject,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";

export type ThemeChromeGroupKey = "statusBar" | "navigationBar";

function themeCursorDefaults(): Record<string, JsonValue> {
  return {
    style: "bar",
    width: 2,
    blinkFrames: 15,
  };
}

function normalizeThemeCursorGroup(value: unknown) {
  const root = isJsonObject(value as JsonValue)
    ? (value as Record<string, JsonValue>)
    : {};
  return {
    ...themeCursorDefaults(),
    ...root,
  };
}

function normalizeChromeBackground(value: unknown, dark = false) {
  if (value === "transparent" || value === undefined || value === null) {
    return dark ? "rgba(0,0,0,0)" : "rgba(255,255,255,0)";
  }
  return value;
}

function themeChromeDefaults(
  groupKey: ThemeChromeGroupKey,
  family: string,
  dark = false,
): Record<string, JsonValue> {
  const isAndroid = family.toLowerCase() === "android";
  const foreground = dark ? "#FFFFFF" : "#000000";
  if (groupKey === "statusBar") {
    return {
      type: isAndroid ? "android-default" : "ios-default",
      foreground,
      background: normalizeChromeBackground(undefined, dark) as string,
      iconScale: 1,
    };
  }
  return {
    type: isAndroid ? "android-gesture" : "ios-home-indicator",
    foreground,
    background: normalizeChromeBackground(undefined, dark) as string,
    iconScale: 1,
  };
}

function normalizeThemeChromeGroup(
  groupKey: ThemeChromeGroupKey,
  family: string,
  value: unknown,
  dark = false,
) {
  const root = isJsonObject(value as JsonValue)
    ? (value as Record<string, JsonValue>)
    : {};
  return {
    ...themeChromeDefaults(groupKey, family, dark),
    ...root,
    background: normalizeChromeBackground(root.background, dark) as JsonValue,
  };
}

function themeKeyboardDefaults(dark = false): Record<string, JsonValue> {
  return dark
    ? {
        background: "#2C2C2E",
        keyBackground: "#636366",
        specialKeyBackground: "#3A3A3C",
        pressedKeyBackground: "#8E8E93",
        popoverBackground: "#636366",
        text: "#FFFFFF",
      }
    : {
        background: "#D1D5DB",
        keyBackground: "#FFFFFF",
        specialKeyBackground: "#AEB4BE",
        pressedKeyBackground: "#8E8E93",
        popoverBackground: "#FFFFFF",
        text: "#000000",
      };
}

function normalizeThemeKeyboardGroup(value: unknown, dark = false) {
  const root = isJsonObject(value as JsonValue)
    ? (value as Record<string, JsonValue>)
    : {};
  return {
    ...themeKeyboardDefaults(dark),
    ...root,
  };
}

export function normalizedThemeTokenRoot({
  root,
  family,
}: {
  root: Record<string, unknown>;
  family: string;
}) {
  const modes = isJsonObject(root.modes as JsonValue)
    ? (root.modes as Record<string, JsonValue>)
    : {};
  const lightMode = isJsonObject(modes.light)
    ? (modes.light as Record<string, JsonValue>)
    : {};
  const darkMode = isJsonObject(modes.dark)
    ? (modes.dark as Record<string, JsonValue>)
    : {};
  const legacyNotifications = isJsonObject(root.notifications as JsonValue)
    ? (root.notifications as Record<string, JsonValue>)
    : {};
  const visibleNotifications = {
    ...legacyNotifications,
  };
  delete visibleNotifications.background;
  delete visibleNotifications.titleColor;
  delete visibleNotifications.bodyColor;
  const lightNotifications = isJsonObject(lightMode.notifications)
    ? (lightMode.notifications as Record<string, JsonValue>)
    : {};
  const darkNotifications = isJsonObject(darkMode.notifications)
    ? (darkMode.notifications as Record<string, JsonValue>)
    : {};
  return {
    ...root,
    notifications: visibleNotifications,
    statusBar: normalizeThemeChromeGroup("statusBar", family, root.statusBar),
    navigationBar: normalizeThemeChromeGroup(
      "navigationBar",
      family,
      root.navigationBar,
    ),
    keyboard: normalizeThemeKeyboardGroup(root.keyboard),
    cursor: normalizeThemeCursorGroup(root.cursor),
    modes: {
      ...modes,
      light: {
        ...lightMode,
        statusBar: normalizeThemeChromeGroup(
          "statusBar",
          family,
          lightMode.statusBar,
        ),
        navigationBar: normalizeThemeChromeGroup(
          "navigationBar",
          family,
          lightMode.navigationBar,
        ),
        keyboard: normalizeThemeKeyboardGroup(lightMode.keyboard),
        cursor: {
          color:
            isJsonObject(lightMode.cursor) &&
            typeof lightMode.cursor.color === "string"
              ? lightMode.cursor.color
              : "#007AFF",
        },
        notifications: {
          background:
            lightNotifications.background ??
            legacyNotifications.background ??
            "rgba(245,245,247,0.92)",
          titleColor:
            lightNotifications.titleColor ??
            legacyNotifications.titleColor ??
            "#000000",
          bodyColor:
            lightNotifications.bodyColor ??
            legacyNotifications.bodyColor ??
            "#3A3A3C",
        },
      },
      dark: {
        ...darkMode,
        statusBar: normalizeThemeChromeGroup(
          "statusBar",
          family,
          darkMode.statusBar,
          true,
        ),
        navigationBar: normalizeThemeChromeGroup(
          "navigationBar",
          family,
          darkMode.navigationBar,
          true,
        ),
        keyboard: normalizeThemeKeyboardGroup(darkMode.keyboard, true),
        cursor: {
          color:
            isJsonObject(darkMode.cursor) &&
            typeof darkMode.cursor.color === "string"
              ? darkMode.cursor.color
              : "#0A84FF",
        },
        notifications: {
          background:
            darkNotifications.background ??
            legacyNotifications.background ??
            "rgba(44,44,46,0.92)",
          titleColor:
            darkNotifications.titleColor ??
            legacyNotifications.titleColor ??
            "#FFFFFF",
          bodyColor:
            darkNotifications.bodyColor ??
            legacyNotifications.bodyColor ??
            "#D1D1D6",
        },
      },
    },
  } as Record<string, JsonValue>;
}

interface ThemeCursorGroupEditorProps {
  tokenRoot: Record<string, JsonValue>;
  onTokenRootChange: (nextRoot: JsonValue) => void;
}

export function ThemeCursorGroupEditor({
  tokenRoot,
  onTokenRootChange,
}: ThemeCursorGroupEditorProps) {
  const group = isJsonObject(tokenRoot.cursor)
    ? (tokenRoot.cursor as Record<string, JsonValue>)
    : themeCursorDefaults();

  function updateCursor(path: JsonPath, nextValue: JsonValue) {
    onTokenRootChange(
      setAtPath(tokenRoot as JsonValue, ["cursor", ...path], nextValue),
    );
  }

  return (
    <div className="theme-chrome-editor">
      <InspectorFieldRow
        className="record-editor-field"
        label={<span>Width</span>}
        control={
          <input
            type="number"
            min={1}
            step={1}
            value={Number(group.width ?? 2)}
            onChange={(event) =>
              updateCursor(["width"], Number(event.target.value))
            }
          />
        }
      />
      <InspectorFieldRow
        className="record-editor-field"
        label={<span>Blink frames</span>}
        control={
          <input
            type="number"
            min={1}
            step={1}
            value={Number(group.blinkFrames ?? 15)}
            onChange={(event) =>
              updateCursor(["blinkFrames"], Number(event.target.value))
            }
          />
        }
      />
    </div>
  );
}

interface ThemeChromeGroupEditorProps {
  tokenRoot: Record<string, JsonValue>;
  groupKey: ThemeChromeGroupKey;
  family: string;
  onTokenRootChange: (nextRoot: JsonValue) => void;
}

export function ThemeChromeGroupEditor({
  tokenRoot,
  groupKey,
  family,
  onTokenRootChange,
}: ThemeChromeGroupEditorProps) {
  const group = isJsonObject(tokenRoot[groupKey])
    ? (tokenRoot[groupKey] as Record<string, JsonValue>)
    : themeChromeDefaults(groupKey, family);
  const typeOptions =
    groupKey === "statusBar"
      ? ["dummy-status-bar", "ios-default", "android-default"]
      : [
          "dummy-navigation-bar",
          "ios-home-indicator",
          "android-gesture",
          "android-3-button",
        ];

  function updateChrome(path: JsonPath, nextValue: JsonValue) {
    onTokenRootChange(
      setAtPath(tokenRoot as JsonValue, [groupKey, ...path], nextValue),
    );
  }

  return (
    <div className="theme-chrome-editor">
      <InspectorFieldRow
        className="record-editor-field"
        label={<span>Type</span>}
        control={
          <select
            value={String(group.type ?? typeOptions[0])}
            onChange={(event) => updateChrome(["type"], event.target.value)}
          >
            {typeOptions.map((option) => (
              <option key={option} value={option}>
                {option}
              </option>
            ))}
          </select>
        }
      />
      <InspectorFieldRow
        className="record-editor-field"
        label={<span>Icon scale</span>}
        control={
          <input
            type="number"
            step="0.05"
            value={Number(group.iconScale ?? 1)}
            onChange={(event) =>
              updateChrome(["iconScale"], Number(event.target.value))
            }
          />
        }
      />
    </div>
  );
}
