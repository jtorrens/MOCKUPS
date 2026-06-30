import type { ReactNode } from "react";
import { InspectorFieldRow } from "../../components/inspector/InspectorFieldRow.js";
import type { JsonPath, JsonValue } from "../../components/json-editor/jsonEditorUtils.js";
import { isJsonObject } from "../../components/json-editor/jsonEditorUtils.js";
import type { JsonUiHints } from "../../components/json-editor/uiHints.js";
import { hintForPath } from "../../components/json-editor/uiHints.js";
import { friendlyGroupLabel } from "../../components/json-editor/labels.js";
import { ChatDictionaryFieldRow } from "./ChatDictionaryFieldRow.js";
import {
  contentSummary,
  isPrimitiveContentValue,
} from "./chatContentModel.js";

interface ChatNestedValueEditorProps {
  rootValue: JsonValue;
  groupKey: string;
  path: JsonPath;
  label: string;
  value: JsonValue;
  hints: JsonUiHints;
  onPathChange: (path: JsonPath, value: JsonValue) => void;
  onRootChange: (value: JsonValue) => void;
}

function contentFieldLabel(
  hints: JsonUiHints,
  groupKey: string,
  path: JsonPath,
  fallback: string,
  value: JsonValue,
) {
  return hintForPath(hints, path, value, groupKey).label ?? friendlyGroupLabel(fallback);
}

export function ChatNestedValueEditor({
  rootValue,
  groupKey,
  path,
  label,
  value,
  hints,
  onPathChange,
  onRootChange,
}: ChatNestedValueEditorProps): ReactNode {
  if (isPrimitiveContentValue(value)) {
    const hint = hintForPath(hints, path, value, groupKey);
    if (hint.field) {
      return (
        <ChatDictionaryFieldRow
          key={path.join(".") || label}
          field={hint.field}
          value={value ?? ""}
          onChange={(nextValue) => onPathChange(path, nextValue as JsonValue)}
        />
      );
    }
    return (
      <InspectorFieldRow
        key={path.join(".") || label}
        className="record-editor-content-field-row"
        label={
          <span>{contentFieldLabel(hints, groupKey, path, label, value)}</span>
        }
        control={<span />}
      />
    );
  }

  if (Array.isArray(value)) {
    return (
      <details className="record-editor-content-nested-card" key={path.join(".") || label}>
        <summary>
          <span>{contentFieldLabel(hints, groupKey, path, label, value)}</span>
          <small>{value.length} items</small>
        </summary>
        <div className="record-editor-content-fields">
          {value.map((entry, index) => (
            <ChatNestedValueEditor
              key={[...path, index].join(".")}
              rootValue={rootValue}
              groupKey={groupKey}
              path={[...path, index]}
              label={`[${index}]`}
              value={entry}
              hints={hints}
              onPathChange={onPathChange}
              onRootChange={onRootChange}
            />
          ))}
        </div>
      </details>
    );
  }

  return (
    <details className="record-editor-content-nested-card" key={path.join(".") || label}>
      <summary>
        <span>{contentFieldLabel(hints, groupKey, path, label, value)}</span>
        <small>{contentSummary(value, groupKey)}</small>
      </summary>
      <div className="record-editor-content-fields">
        {Object.entries(value).map(([key, entryValue]) => (
          <ChatNestedValueEditor
            key={[...path, key].join(".")}
            rootValue={rootValue}
            groupKey={groupKey}
            path={[...path, key]}
            label={key}
            value={isJsonObject(entryValue) ? entryValue : entryValue}
            hints={hints}
            onPathChange={onPathChange}
            onRootChange={onRootChange}
          />
        ))}
      </div>
    </details>
  );
}
