import { DeferredNumberInput } from "../../editor-ui/DeferredNumberInput.js";
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
import type { JsonUiHints } from "./uiHints.js";
import { hintForPath } from "./uiHints.js";
import {
  defaultJsonValue,
  getAtPath,
  isJsonObject,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "./jsonEditorUtils.js";

interface JsonValueEditorProps {
  rootValue: JsonValue;
  path: JsonPath;
  value: JsonValue;
  hints: JsonUiHints;
  groupContext?: string;
  productionFontCatalog?: ProductionFontCatalog;
  paletteCatalog?: PaletteColorCatalog;
  onChange: (nextValue: JsonValue) => void;
  onRootChange?: (nextValue: JsonValue) => void;
}

function isAlphaColorField(key: string, parent: string, groupContext?: string) {
  const context = `${parent} ${groupContext ?? ""}`;
  return (
    (key === "background" &&
      /(statusbar|statusBar|navigationbar|navigationBar|notification|notifications)/i.test(
        context,
      )) ||
    (key === "color" && /(shadow|shadows)/i.test(context))
  );
}

function chromeTypeOptions(context?: string) {
  if (/navigationbar|navigationBar/i.test(context ?? "")) {
    return ["dummy-navigation-bar", "ios-home-indicator", "android-gesture", "android-3-button"];
  }
  if (/statusbar|statusBar/i.test(context ?? "")) {
    return ["dummy-status-bar", "ios-default", "android-default"];
  }
  return [];
}

function withCurrentOption(options: string[], value: JsonValue) {
  const current = value === null || value === undefined ? "" : String(value);
  if (!current || options.includes(current)) return options;
  return [current, ...options];
}

function isNeutralHueField(path: JsonPath) {
  return (
    String(path[path.length - 2] ?? "") === "neutralTint" &&
    String(path[path.length - 1] ?? "") === "hueDeg"
  );
}

function normalizeHueDeg(value: number) {
  return ((value % 360) + 360) % 360;
}

function fontFamilyForPath(rootValue: JsonValue, path: JsonPath): string | undefined {
  for (let length = path.length - 1; length >= 0; length -= 1) {
    const parentPath = path.slice(0, length);
    const parent = getAtPath(rootValue, parentPath);
    if (!isJsonObject(parent)) continue;
    const explicitFamily = parent.fontFamily ?? parent.family;
    if (typeof explicitFamily === "string" && explicitFamily.trim()) {
      return explicitFamily;
    }
  }
  return undefined;
}

function isFontStyleKey(key: string, parent: string) {
  return key === "fontStyle" || (key === "style" && /font|type/i.test(parent));
}

function isFontWeightKey(key: string, parent: string, groupContext?: string) {
  return (
    /fontWeight$/i.test(key) ||
    /Weight$/i.test(key) ||
    (key === "weight" && /font/i.test(parent || groupContext || ""))
  );
}

function isFontFamilyKey(key: string, parent: string, groupContext?: string) {
  return (
    /fontFamily$/i.test(key) ||
    (key === "family" && /font|fonts/i.test(parent || groupContext || ""))
  );
}

function withProductionFontMetadata(
  rootValue: JsonValue,
  path: JsonPath,
  family: string,
  productionFontCatalog: ProductionFontCatalog | undefined,
) {
  const productionFontId = productionFontIdForFamily(
    productionFontCatalog,
    family,
  );
  if (!productionFontId) return rootValue;
  const parentPath = path.slice(0, -1);
  return setAtPath(
    setAtPath(rootValue, [...parentPath, "productionFontId"], productionFontId),
    [...parentPath, "source"],
    "production_font_family",
  );
}

function withProductionFontSelection(
  rootValue: JsonValue,
  path: JsonPath,
  selection: ProductionFontSelection,
  productionFontCatalog: ProductionFontCatalog | undefined,
  options: { lockFamily?: boolean } = {},
) {
  const parentPath = path.slice(0, -1);
  let nextRoot = options.lockFamily
    ? rootValue
    : setAtPath(rootValue, path, selection.fontFamily);
  nextRoot = setAtPath(nextRoot, [...parentPath, "fontWeight"], selection.fontWeight);
  nextRoot = setAtPath(nextRoot, [...parentPath, "fontStyle"], selection.fontStyle);
  if (options.lockFamily) return nextRoot;
  return withProductionFontMetadata(
    nextRoot,
    path,
    selection.fontFamily,
    productionFontCatalog,
  );
}

export function JsonValueEditor({
  rootValue,
  path,
  value,
  hints,
  groupContext,
  productionFontCatalog,
  paletteCatalog,
  onChange,
  onRootChange,
}: JsonValueEditorProps) {
  const hint = hintForPath(hints, path, value, groupContext);
  const widget = hint.widget;
  const key = String(path[path.length - 1] ?? "");
  const parent = String(path[path.length - 2] ?? "");

  const parentChromeOptions = key === "type" ? chromeTypeOptions(parent) : [];
  const contextChromeOptions =
    key === "type" ? chromeTypeOptions(groupContext) : [];
  const chromeOptions = parentChromeOptions.length
    ? parentChromeOptions
    : contextChromeOptions;
  const dynamicSelectOptions =
    hint.options ?? (chromeOptions.length ? chromeOptions : []);

  const resolvedWidget =
    widget ||
    (dynamicSelectOptions.length ? "select" : undefined) ||
    (isFontFamilyKey(key, parent, groupContext) ? "font" : undefined);

  if (resolvedWidget === "select" && dynamicSelectOptions.length) {
    return (
      <select
        className="json-value-control"
        value={String(value ?? "")}
        onChange={(event) => onChange(event.target.value)}
      >
        {withCurrentOption(dynamicSelectOptions, value).map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    );
  }

  if (resolvedWidget === "font") {
    const parentPath = path.slice(0, -1);
    const parentValue = getAtPath(rootValue, parentPath);
    const parentObject = isJsonObject(parentValue) ? parentValue : {};
    return (
      <ProductionFontSelector
        compact
        catalog={productionFontCatalog}
        lockFamily={hint.lockFontFamily}
        value={{
          fontFamily: value,
          fontWeight: parentObject.fontWeight,
          fontStyle: parentObject.fontStyle,
        }}
        onChange={(nextFont) => {
          if (!onRootChange) {
            onChange(nextFont.fontFamily);
            return;
          }
          onRootChange(
            withProductionFontSelection(
              rootValue,
              path,
              nextFont,
              productionFontCatalog,
              { lockFamily: hint.lockFontFamily },
            ),
          );
        }}
      />
    );
  }

  if (isFontWeightKey(key, parent, groupContext) || isFontStyleKey(key, parent)) {
    const parentPath = path.slice(0, -1);
    const parentValue = getAtPath(rootValue, parentPath);
    const parentObject = isJsonObject(parentValue) ? parentValue : {};
    return (
      <ProductionFontSelector
        compact
        catalog={productionFontCatalog}
        lockFamily={hint.lockFontFamily}
        value={{
          fontFamily: fontFamilyForPath(rootValue, path),
          fontWeight: parentObject.fontWeight,
          fontStyle: parentObject.fontStyle,
        }}
        onChange={(nextFont) =>
          onRootChange?.(
            withProductionFontSelection(
              rootValue,
              [...parentPath, "fontFamily"],
              nextFont,
              productionFontCatalog,
              { lockFamily: hint.lockFontFamily },
            ),
          )
        }
      />
    );
  }

  if (
    typeof value === "string" &&
    (widget === "color" || isAlphaColorField(key, parent, groupContext))
  ) {
    return (
      <ColorValueEditor
        value={value}
        alpha={isAlphaColorField(key, parent, groupContext)}
        label={key}
        paletteCatalog={paletteCatalog}
        onChange={onChange}
      />
    );
  }

  if (typeof value === "boolean") {
    return (
      <label className="json-checkbox">
        <input
          type="checkbox"
          checked={value}
          onChange={(event) => onChange(event.target.checked)}
        />
        {value ? "true" : "false"}
      </label>
    );
  }

  if (typeof value === "number" && isNeutralHueField(path)) {
    const hue = normalizeHueDeg(value);
    return (
      <div className="json-hue-slider-control">
        <input
          aria-label="Neutral tint hue"
          className="json-hue-slider"
          max={360}
          min={0}
          step={1}
          type="range"
          value={hue}
          style={{ accentColor: `hsl(${hue} 80% 52%)` }}
          onChange={(event) => onChange(Number(event.currentTarget.value))}
        />
        <DeferredNumberInput
          ariaLabel="Neutral tint hue degrees"
          className="json-value-control json-hue-slider-value"
          max={360}
          min={0}
          step={1}
          value={hue}
          onCommit={(nextValue) => onChange(normalizeHueDeg(nextValue))}
        />
        <span className="json-hue-slider-unit">°</span>
      </div>
    );
  }

  if (typeof value === "number") {
    return (
      <DeferredNumberInput
        className="json-value-control"
        min={hint.min}
        max={hint.max}
        step={hint.step ?? "any"}
        value={value}
        onCommit={(nextValue) => onChange(nextValue)}
      />
    );
  }

  if (value === null) {
    return (
      <span className="json-null-editor">
        <code>null</code>
        <select
          aria-label="Convert null"
          onChange={(event) => onChange(defaultJsonValue(event.target.value))}
          value="null"
        >
          <option value="null">null</option>
          <option value="string">string</option>
          <option value="number">number</option>
          <option value="boolean">boolean</option>
          <option value="object">object</option>
          <option value="array">array</option>
        </select>
      </span>
    );
  }

  if (widget === "textarea") {
    return (
      <textarea
        className="json-value-textarea"
        value={typeof value === "string" ? value : String(value ?? "")}
        onChange={(event) => onChange(event.target.value)}
      />
    );
  }

  return (
    <input
      className="json-value-control"
      type="text"
      value={typeof value === "string" ? value : String(value ?? "")}
      onChange={(event) => onChange(event.target.value)}
    />
  );
}
