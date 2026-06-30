import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import { TypographySelector } from "../editor-ui/TypographySelector.js";
import type { ProductionFontCatalog } from "../components/json-editor/productionFonts.js";
import { THEME_FIELDS } from "../../domain/fields/themeFields.js";
import type { FieldDefinition } from "../../domain/value-system/index.js";
import {
  controlDefinitionForField,
  editorMetadataForField,
  type EditorControlKind,
} from "../editor-ui/ValueKindControlRegistry.js";
import {
  DICTIONARY_CONTROL_CLASS,
  DICTIONARY_FIELD_CLASS,
} from "../editor-ui/DictionaryFieldControl.js";
import {
  isJsonObject,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";

export type ThemeChromeGroupKey = "statusBar" | "navigationBar";

function fontObject(root: Record<string, JsonValue>) {
  return isJsonObject(root.fonts as JsonValue)
    ? (root.fonts as Record<string, JsonValue>)
    : {};
}

function themeFieldMetadata(
  field: FieldDefinition,
  expectedControl: EditorControlKind,
) {
  const control = controlDefinitionForField(field).control;
  if (control !== expectedControl) {
    throw new Error(
      `Theme field "${field.id}" expected ${expectedControl} control, got ${control}`,
    );
  }
  return editorMetadataForField(field);
}

const dictionaryThemeRowClassName = `record-editor-field ${DICTIONARY_FIELD_CLASS}`;
const dictionaryThemeControlClassName = `json-value-control ${DICTIONARY_CONTROL_CLASS}`;

function numberValue(value: unknown, fallback: number): number {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function normalizeHueDeg(value: number) {
  return ((value % 360) + 360) % 360;
}

function normalizeNeutralTint(value: unknown): Record<string, JsonValue> {
  const root = isJsonObject(value as JsonValue)
    ? (value as Record<string, JsonValue>)
    : {};
  return {
    hueDeg: numberValue(root.hueDeg, 0),
    saturation: numberValue(
      root.saturation,
      THEME_FIELDS.neutralTintSaturation.defaultValue as number,
    ),
  };
}

function themeCursorDefaults(): Record<string, JsonValue> {
  return {
    style: "bar",
    width: THEME_FIELDS.cursorWidth.defaultValue as number,
    blinkFrames: THEME_FIELDS.cursorBlinkFrames.defaultValue as number,
  };
}

function normalizeThemeCursorGroup(value: unknown) {
  const root = isJsonObject(value as JsonValue)
    ? (value as Record<string, JsonValue>)
    : {};
  const { color: _legacyColor, ...cursorRoot } = root;
  return {
    ...themeCursorDefaults(),
    ...cursorRoot,
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

function themeSurfaceReliefDefaults(): Record<string, JsonValue> {
  return {
    default: {
      angleDeg: THEME_FIELDS.surfaceReliefAngleDeg.defaultValue as number,
      extension: THEME_FIELDS.surfaceReliefExtension.defaultValue as number,
      spread: THEME_FIELDS.surfaceReliefSpread.defaultValue as number,
      upperIntensity: THEME_FIELDS.surfaceReliefUpperIntensity.defaultValue as number,
      lowerIntensity: THEME_FIELDS.surfaceReliefLowerIntensity.defaultValue as number,
    },
  };
}

function normalizeThemeSurfaceReliefGroup(value: unknown) {
  const root = isJsonObject(value as JsonValue)
    ? (value as Record<string, JsonValue>)
    : {};
  const defaultRoot = isJsonObject(root.default)
    ? (root.default as Record<string, JsonValue>)
    : {};
  const defaults = themeSurfaceReliefDefaults();
  const defaultDefaults = defaults.default as Record<string, JsonValue>;
  return {
    ...defaults,
    ...root,
    default: {
      ...defaultDefaults,
      ...defaultRoot,
    },
  };
}

export function normalizedThemeTokenRoot({
  root,
  family,
}: {
  root: Record<string, unknown>;
  family: string;
}) {
  const semanticColorDefaults = {
    "debug.red": "debug_red",
    "icons.primary": "gray_000",
    "icons.secondary": "gray_040",
    "icons.accent": "blue",
    "borders.primary": "gray_080",
    "borders.secondary": "gray_070",
    "borders.alternate": "gray_090",
  };
  const modes = isJsonObject(root.modes as JsonValue)
    ? (root.modes as Record<string, JsonValue>)
    : {};
  const lightMode = isJsonObject(modes.light)
    ? (modes.light as Record<string, JsonValue>)
    : {};
  const darkMode = isJsonObject(modes.dark)
    ? (modes.dark as Record<string, JsonValue>)
    : {};
  const rootColors = isJsonObject(root.colors as JsonValue)
    ? (root.colors as Record<string, JsonValue>)
    : {};
  const lightColors = isJsonObject(lightMode.colors)
    ? (lightMode.colors as Record<string, JsonValue>)
    : {};
  const darkColors = isJsonObject(darkMode.colors)
    ? (darkMode.colors as Record<string, JsonValue>)
    : {};
  return {
    ...root,
    neutralTint: isJsonObject(root.neutralTint as JsonValue)
      ? root.neutralTint
      : {
          hueDeg: 0,
          saturation: 0,
        },
    colors: {
      ...semanticColorDefaults,
      ...rootColors,
    },
    fonts: {
      ...(isJsonObject(root.fonts as JsonValue)
        ? (root.fonts as Record<string, JsonValue>)
        : {}),
      emojiFamily:
        isJsonObject(root.fonts as JsonValue) &&
        typeof (root.fonts as Record<string, JsonValue>).emojiFamily === "string"
          ? (root.fonts as Record<string, JsonValue>).emojiFamily
          : "",
    },
    statusBar: normalizeThemeChromeGroup("statusBar", family, root.statusBar),
    navigationBar: normalizeThemeChromeGroup(
      "navigationBar",
      family,
      root.navigationBar,
    ),
    keyboard: normalizeThemeKeyboardGroup(root.keyboard),
    cursor: normalizeThemeCursorGroup(root.cursor),
    surfaceRelief: normalizeThemeSurfaceReliefGroup(root.surfaceRelief),
    modes: {
      ...modes,
      light: {
        ...lightMode,
        colors: {
          ...semanticColorDefaults,
          ...rootColors,
          ...lightColors,
        },
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
        cursor: normalizeThemeCursorGroup(lightMode.cursor),
      },
      dark: {
        ...darkMode,
        colors: {
          ...semanticColorDefaults,
          ...rootColors,
          ...darkColors,
        },
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
        cursor: normalizeThemeCursorGroup(darkMode.cursor),
      },
    },
  } as Record<string, JsonValue>;
}

interface ThemeSurfaceReliefGroupEditorProps {
  tokenRoot: Record<string, JsonValue>;
  onTokenRootChange: (nextRoot: JsonValue) => void;
}

interface NeutralTintGroupEditorProps {
  tokenRoot: Record<string, JsonValue>;
  onTokenRootChange: (nextRoot: JsonValue) => void;
}

export function NeutralTintGroupEditor({
  tokenRoot,
  onTokenRootChange,
}: NeutralTintGroupEditorProps) {
  const group = normalizeNeutralTint(tokenRoot.neutralTint);
  const hueField = themeFieldMetadata(
    THEME_FIELDS.neutralTintHueDeg,
    "number",
  );
  const saturationField = themeFieldMetadata(
    THEME_FIELDS.neutralTintSaturation,
    "alpha",
  );
  const hue = normalizeHueDeg(
    numberValue(
      group.hueDeg,
      THEME_FIELDS.neutralTintHueDeg.defaultValue as number,
    ),
  );
  const saturation = Math.max(
    0,
    Math.min(
      1,
      numberValue(
        group.saturation,
        THEME_FIELDS.neutralTintSaturation.defaultValue as number,
      ),
    ),
  );

  function updateNeutralTint(path: JsonPath, nextValue: JsonValue) {
    onTokenRootChange(
      setAtPath(tokenRoot as JsonValue, ["neutralTint", ...path], nextValue),
    );
  }

  return (
    <div className="theme-chrome-editor">
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{hueField.label}</span>}
        control={
          <div className="json-hue-slider-control">
            <input
              aria-label="Neutral tint hue"
              className="json-hue-slider"
              max={hueField.max}
              min={hueField.min}
              step={hueField.step ?? 1}
              type="range"
              value={hue}
              style={{ accentColor: `hsl(${hue} 80% 52%)` }}
              onChange={(event) =>
                updateNeutralTint(
                  ["hueDeg"],
                  Number(event.currentTarget.value),
                )
              }
            />
            <input
              aria-label="Neutral tint hue degrees"
              className={`${dictionaryThemeControlClassName} json-hue-slider-value`}
              max={hueField.max}
              min={hueField.min}
              step={hueField.step ?? 1}
              type="number"
              value={hue}
              onChange={(event) =>
                updateNeutralTint(
                  ["hueDeg"],
                  normalizeHueDeg(Number(event.currentTarget.value)),
                )
              }
            />
            <span className="json-hue-slider-unit">°</span>
          </div>
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{saturationField.label}</span>}
        control={
          <input
            className={dictionaryThemeControlClassName}
            max={saturationField.max}
            min={saturationField.min}
            step={saturationField.step ?? 0.01}
            type="number"
            value={saturation}
            onChange={(event) =>
              updateNeutralTint(
                ["saturation"],
                Math.max(0, Math.min(1, Number(event.currentTarget.value))),
              )
            }
          />
        }
      />
    </div>
  );
}

export function ThemeSurfaceReliefGroupEditor({
  tokenRoot,
  onTokenRootChange,
}: ThemeSurfaceReliefGroupEditorProps) {
  const surfaceRelief = normalizeThemeSurfaceReliefGroup(tokenRoot.surfaceRelief);
  const group = isJsonObject(surfaceRelief.default)
    ? (surfaceRelief.default as Record<string, JsonValue>)
    : {};
  const angleField = themeFieldMetadata(
    THEME_FIELDS.surfaceReliefAngleDeg,
    "number",
  );
  const extensionField = themeFieldMetadata(
    THEME_FIELDS.surfaceReliefExtension,
    "number",
  );
  const spreadField = themeFieldMetadata(
    THEME_FIELDS.surfaceReliefSpread,
    "number",
  );
  const upperIntensityField = themeFieldMetadata(
    THEME_FIELDS.surfaceReliefUpperIntensity,
    "number",
  );
  const lowerIntensityField = themeFieldMetadata(
    THEME_FIELDS.surfaceReliefLowerIntensity,
    "number",
  );

  function updateSurfaceRelief(path: JsonPath, nextValue: JsonValue) {
    onTokenRootChange(
      setAtPath(
        tokenRoot as JsonValue,
        ["surfaceRelief", "default", ...path],
        nextValue,
      ),
    );
  }

  return (
    <div className="theme-chrome-editor">
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{angleField.label}</span>}
        control={
          <input
            className={dictionaryThemeControlClassName}
            min={angleField.min}
            max={angleField.max}
            type="number"
            step={angleField.step ?? 1}
            value={Number(group.angleDeg ?? THEME_FIELDS.surfaceReliefAngleDeg.defaultValue)}
            onChange={(event) =>
              updateSurfaceRelief(["angleDeg"], Number(event.target.value))
            }
          />
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{extensionField.label}</span>}
        control={
          <input
            className={dictionaryThemeControlClassName}
            min={extensionField.min}
            max={extensionField.max}
            type="number"
            step={extensionField.step ?? "any"}
            value={Number(group.extension ?? THEME_FIELDS.surfaceReliefExtension.defaultValue)}
            onChange={(event) =>
              updateSurfaceRelief(["extension"], Number(event.target.value))
            }
          />
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{spreadField.label}</span>}
        control={
          <input
            className={dictionaryThemeControlClassName}
            min={spreadField.min}
            max={spreadField.max}
            type="number"
            step={spreadField.step ?? "any"}
            value={Number(group.spread ?? THEME_FIELDS.surfaceReliefSpread.defaultValue)}
            onChange={(event) =>
              updateSurfaceRelief(["spread"], Number(event.target.value))
            }
          />
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{upperIntensityField.label}</span>}
        control={
          <input
            className={dictionaryThemeControlClassName}
            min={upperIntensityField.min}
            max={upperIntensityField.max}
            type="number"
            step={upperIntensityField.step ?? "any"}
            value={Number(group.upperIntensity ?? THEME_FIELDS.surfaceReliefUpperIntensity.defaultValue)}
            onChange={(event) =>
              updateSurfaceRelief(["upperIntensity"], Number(event.target.value))
            }
          />
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{lowerIntensityField.label}</span>}
        control={
          <input
            className={dictionaryThemeControlClassName}
            min={lowerIntensityField.min}
            max={lowerIntensityField.max}
            type="number"
            step={lowerIntensityField.step ?? "any"}
            value={Number(group.lowerIntensity ?? THEME_FIELDS.surfaceReliefLowerIntensity.defaultValue)}
            onChange={(event) =>
              updateSurfaceRelief(["lowerIntensity"], Number(event.target.value))
            }
          />
        }
      />
    </div>
  );
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
  const widthField = themeFieldMetadata(THEME_FIELDS.cursorWidth, "number");
  const blinkFramesField = themeFieldMetadata(
    THEME_FIELDS.cursorBlinkFrames,
    "number",
  );

  function updateCursor(path: JsonPath, nextValue: JsonValue) {
    onTokenRootChange(
      setAtPath(tokenRoot as JsonValue, ["cursor", ...path], nextValue),
    );
  }

  return (
    <div className="theme-chrome-editor">
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{widthField.label}</span>}
        control={
          <input
            className={dictionaryThemeControlClassName}
            type="number"
            min={widthField.min}
            max={widthField.max}
            step={widthField.step ?? 1}
            value={Number(group.width ?? THEME_FIELDS.cursorWidth.defaultValue)}
            onChange={(event) =>
              updateCursor(["width"], Number(event.target.value))
            }
          />
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{blinkFramesField.label}</span>}
        control={
          <input
            className={dictionaryThemeControlClassName}
            type="number"
            min={blinkFramesField.min}
            max={blinkFramesField.max}
            step={blinkFramesField.step ?? 1}
            value={Number(
              group.blinkFrames ?? THEME_FIELDS.cursorBlinkFrames.defaultValue,
            )}
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

interface ThemeFontsGroupEditorProps {
  tokenRoot: Record<string, JsonValue>;
  productionFontCatalog?: ProductionFontCatalog;
  onTokenRootChange: (nextRoot: JsonValue) => void;
}

export function ThemeFontsGroupEditor({
  tokenRoot,
  productionFontCatalog,
  onTokenRootChange,
}: ThemeFontsGroupEditorProps) {
  const fonts = fontObject(tokenRoot);

  function updateFonts(path: JsonPath, nextValue: JsonValue) {
    onTokenRootChange(
      setAtPath(tokenRoot as JsonValue, ["fonts", ...path], nextValue),
    );
  }

  function updateFontSelection(nextFont: {
    fontFamily: string;
    fontWeight: number;
    fontStyle: "normal" | "italic";
  }) {
    onTokenRootChange(
      setAtPath(
        setAtPath(
          setAtPath(tokenRoot as JsonValue, ["fonts", "family"], nextFont.fontFamily),
          ["fonts", "fontWeight"],
          nextFont.fontWeight,
        ),
        ["fonts", "fontStyle"],
        nextFont.fontStyle,
      ),
    );
  }

  return (
    <div className="theme-chrome-editor">
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{THEME_FIELDS.typographyFamily.ui?.label ?? "Font family"}</span>}
        control={
          <TypographySelector
            compact
            catalog={productionFontCatalog}
            category="normal"
            controlClassName={dictionaryThemeControlClassName}
            value={{
              fontFamily: fonts.family,
              fontWeight: fonts.fontWeight ?? fonts.weight,
              fontStyle: fonts.fontStyle,
            }}
            onChange={updateFontSelection}
          />
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={
          <span>{THEME_FIELDS.typographyEmojiFamily.ui?.label ?? "Emoji font"}</span>
        }
        control={
          <TypographySelector
            compact
            familyOnly
            catalog={productionFontCatalog}
            category="emoji"
            controlClassName={dictionaryThemeControlClassName}
            value={{
              fontFamily: fonts.emojiFamily,
              fontWeight: 400,
              fontStyle: "normal",
            }}
            onChange={(nextFont) =>
              updateFonts(["emojiFamily"], nextFont.fontFamily)
            }
          />
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={<span>{THEME_FIELDS.typographyBodySize.ui?.label ?? "Body size"}</span>}
        control={
          <input
            className={dictionaryThemeControlClassName}
            type="number"
            min={1}
            step={1}
            value={Number(fonts.bodySize ?? 17)}
            onChange={(event) => updateFonts(["bodySize"], Number(event.target.value))}
          />
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={
          <span>
            {THEME_FIELDS.typographyBodyLineHeight.ui?.label ?? "Body line height"}
          </span>
        }
        control={
          <input
            className={dictionaryThemeControlClassName}
            type="number"
            min={1}
            step={1}
            value={Number(fonts.bodyLineHeight ?? 21.25)}
            onChange={(event) =>
              updateFonts(["bodyLineHeight"], Number(event.target.value))
            }
          />
        }
      />
      <InspectorFieldRow
        className={dictionaryThemeRowClassName}
        label={
          <span>{THEME_FIELDS.typographyCaptionSize.ui?.label ?? "Caption size"}</span>
        }
        control={
          <input
            className={dictionaryThemeControlClassName}
            type="number"
            min={1}
            step={1}
            value={Number(fonts.captionSize ?? 13)}
            onChange={(event) =>
              updateFonts(["captionSize"], Number(event.target.value))
            }
          />
        }
      />
    </div>
  );
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
