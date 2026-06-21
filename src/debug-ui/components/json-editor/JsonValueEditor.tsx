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

export function JsonValueEditor({
  path,
  value,
  hints,
  onChange,
}: JsonValueEditorProps) {
  const hint = hintForPath(hints, path, value);
  const widget = hint.widget;

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

  if (widget === "color" && typeof value === "string") {
    return (
      <span className="json-color-pair">
        <input
          aria-label="Color picker"
          type="color"
          value={/^#[0-9a-fA-F]{6}$/.test(value) ? value : "#000000"}
          onChange={(event) => onChange(event.target.value)}
        />
        <input
          className="json-value-control"
          value={value}
          onChange={(event) => onChange(event.target.value)}
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
