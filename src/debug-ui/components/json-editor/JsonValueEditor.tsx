import { useSystemFontCatalog } from "./systemFonts.js";
import { DeferredNumberInput } from "../../editor-ui/DeferredNumberInput.js";
import { ColorValueEditor } from "./ColorValueEditor.js";
import {
  fontStylesForFamily,
  productionFontIdForFamily,
  type ProductionFontCatalog,
} from "./productionFonts.js";
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
  return key === "style" && /font|type/i.test(parent);
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

function firstAvailableFontStyle(
  productionFontCatalog: ProductionFontCatalog | undefined,
  stylesByFamily: Map<string, string[]>,
  family: string,
) {
  return (
    fontStylesForFamily(productionFontCatalog, stylesByFamily, family)[0] ??
    "Regular"
  );
}

function withCompatibleSiblingWeights(
  rootValue: JsonValue,
  path: JsonPath,
  family: string,
  productionFontCatalog: ProductionFontCatalog | undefined,
  stylesByFamily: Map<string, string[]>,
) {
  const parentPath = path.slice(0, -1);
  const parent = getAtPath(rootValue, parentPath);
  if (!isJsonObject(parent)) return rootValue;
  const options = fontStylesForFamily(
    productionFontCatalog,
    stylesByFamily,
    family,
  );
  const fallback = firstAvailableFontStyle(
    productionFontCatalog,
    stylesByFamily,
    family,
  );
  let nextRoot = rootValue;
  for (const [key, value] of Object.entries(parent)) {
    if (!isFontWeightKey(key, String(parentPath[parentPath.length - 1] ?? ""))) {
      continue;
    }
    if (typeof value === "string" && options.includes(value)) {
      continue;
    }
    nextRoot = setAtPath(nextRoot, [...parentPath, key], fallback);
  }
  return nextRoot;
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

export function JsonValueEditor({
  rootValue,
  path,
  value,
  hints,
  groupContext,
  productionFontCatalog,
  onChange,
  onRootChange,
}: JsonValueEditorProps) {
  const hint = hintForPath(hints, path, value, groupContext);
  const widget = hint.widget;
  const { families, stylesByFamily } = useSystemFontCatalog();
  const approvedFamilies = productionFontCatalog?.families ?? [];
  const fontFamilies = productionFontCatalog ? approvedFamilies : families;
  const key = String(path[path.length - 1] ?? "");
  const parent = String(path[path.length - 2] ?? "");

  const parentChromeOptions = key === "type" ? chromeTypeOptions(parent) : [];
  const contextChromeOptions =
    key === "type" ? chromeTypeOptions(groupContext) : [];
  const chromeOptions = parentChromeOptions.length
    ? parentChromeOptions
    : contextChromeOptions;
  const fontOptions =
    isFontStyleKey(key, parent) || isFontWeightKey(key, parent, groupContext)
      ? fontStylesForFamily(
          productionFontCatalog,
          stylesByFamily,
          fontFamilyForPath(rootValue, path),
        )
      : [];
  const dynamicSelectOptions =
    hint.options ?? (chromeOptions.length ? chromeOptions : fontOptions);

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
    return (
      <select
        className="json-value-control"
        value={String(value ?? "")}
        onChange={(event) => {
          const nextFamily = event.target.value;
          if (onRootChange && isFontFamilyKey(key, parent, groupContext)) {
            const nextRoot = withCompatibleSiblingWeights(
              withProductionFontMetadata(
                setAtPath(rootValue, path, nextFamily),
                path,
                nextFamily,
                productionFontCatalog,
              ),
              path,
              nextFamily,
              productionFontCatalog,
              stylesByFamily,
            );
            onRootChange(nextRoot);
            return;
          }
          onChange(nextFamily);
        }}
      >
        {fontFamilies.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    );
  }

  if (isFontWeightKey(key, parent, groupContext) || isFontStyleKey(key, parent)) {
    const options = withCurrentOption(
      fontStylesForFamily(
        productionFontCatalog,
        stylesByFamily,
        fontFamilyForPath(rootValue, path),
      ),
      value,
    );
    return (
      <select
        className="json-value-control"
        value={String(value ?? "")}
        onChange={(event) => onChange(event.target.value)}
      >
        {options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
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
