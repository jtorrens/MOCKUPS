import { InspectorFieldRow } from "../components/inspector/InspectorFieldRow.js";
import {
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "../components/json-editor/jsonEditorUtils.js";
import { parsedObject } from "./recordJsonUtils.js";

function stringifyJson(value: unknown): string {
  return JSON.stringify(value, null, 2);
}

interface ModuleBehaviorFieldsProps {
  rawValue: string;
  onRawChange: (nextRaw: string) => void;
}

export function ModuleBehaviorFields({
  rawValue,
  onRawChange,
}: ModuleBehaviorFieldsProps) {
  const root = parsedObject(rawValue);

  function updateBehaviorValue(path: JsonPath, nextValue: JsonValue) {
    onRawChange(stringifyJson(setAtPath(root as JsonValue, path, nextValue)));
  }

  return (
    <>
      {[
        ["Show header", "showHeader", true],
        ["Show status bar", "showStatusBar", true],
        ["Show keyboard", "showKeyboard", false],
      ].map(([label, key, fallback]) => (
        <InspectorFieldRow
          key={String(key)}
          className="record-editor-field record-editor-field-boolean"
          label={<span>{String(label)}</span>}
          control={
            <input
              type="checkbox"
              checked={Boolean(root[String(key)] ?? fallback)}
              onChange={(event) =>
                updateBehaviorValue([String(key)], event.target.checked)
              }
            />
          }
        />
      ))}
      <InspectorFieldRow
        key="initialScroll"
        className="record-editor-field record-editor-field-string"
        label={<span>Initial scroll</span>}
        control={
          <select
            value={String(root.initialScroll ?? "bottom")}
            onChange={(event) =>
              updateBehaviorValue(["initialScroll"], event.target.value)
            }
          >
            <option value="top">Top</option>
            <option value="bottom">Bottom</option>
            <option value="preserve">Preserve</option>
          </select>
        }
      />
    </>
  );
}
