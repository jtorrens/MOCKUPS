import { fontStylesForFamily, useSystemFontCatalog } from "./systemFonts.js";
import type { JsonUiHints } from "./uiHints.js";
import { hintForPath } from "./uiHints.js";
import {
  coercePrimitiveValue,
  defaultJsonValue,
  type JsonPath,
  type JsonValue,
} from "./jsonEditorUtils.js";

interface JsonValueEditorProps {
  path: JsonPath;
  value: JsonValue;
  hints: JsonUiHints;
  onChange: (nextValue: JsonValue) => void;
}

function isHexColor(value: string): boolean {
  return /^#[0-9a-fA-F]{6}$/.test(value);
}

export function JsonValueEditor({
  path,
  value,
  hints,
  onChange,
}: JsonValueEditorProps) {
  const hint = hintForPath(hints, path, value);
  const widget = hint.widget;
  const { families, stylesByFamily } = useSystemFontCatalog();
  const key = String(path[path.length - 1] ?? "");

  if (widget === "select" && hint.options?.length) {
    return (
      <select
        className="json-value-control"
        value={String(value ?? "")}
        onChange={(event) => onChange(event.target.value)}
      >
        {hint.options.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    );
  }

  if (widget === "font") {
    return (
      <select
        className="json-value-control"
        value={String(value ?? "")}
        onChange={(event) => onChange(event.target.value)}
      >
        {families.map((option) => (
          <option key={option} value={option}>
            {option}
          </option>
        ))}
      </select>
    );
  }

  if (/fontWeight$/i.test(key)) {
    const family = typeof value === "string" ? undefined : undefined;
    const options = fontStylesForFamily(stylesByFamily, family);
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

  if (widget === "color" && typeof value === "string") {
    return (
      <span className="json-color-pair">
        <input
          aria-label="Color picker"
          type="color"
          value={isHexColor(value) ? value : "#000000"}
          onChange={(event) => onChange(event.target.value)}
        />
        <input
          className="json-value-control"
          value={value}
          onChange={(event) => {
            const next = event.target.value;
            onChange(isHexColor(next) ? next.toLowerCase() : next);
          }}
        />
      </span>
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
      <input
        className="json-value-control"
        type="number"
        min={hint.min}
        max={hint.max}
        step={hint.step ?? "any"}
        value={String(value)}
        onChange={(event) => onChange(coercePrimitiveValue(event.target.value, value))}
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
