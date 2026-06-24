import { useState } from "react";
import type { JsonUiHints } from "./uiHints.js";
import {
  defaultJsonValue,
  deleteAtPath,
  renameKeyAtPath,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "./jsonEditorUtils.js";
import { JsonTreeNode } from "./JsonTreeNode.js";

interface JsonObjectEditorProps {
  rootValue: JsonValue;
  inheritedRoot?: JsonValue;
  path: JsonPath;
  value: Record<string, JsonValue>;
  hints: JsonUiHints;
  restoreStrategy: "remove" | "set";
  allowObjectStructuralEdits: boolean;
  allowArrayStructuralEdits: boolean;
  groupContext?: string;
  onRootChange: (nextValue: JsonValue) => void;
}

export function JsonObjectEditor({
  rootValue,
  inheritedRoot,
  path,
  value,
  hints,
  restoreStrategy,
  allowObjectStructuralEdits,
  allowArrayStructuralEdits,
  groupContext,
  onRootChange,
}: JsonObjectEditorProps) {
  const [newKey, setNewKey] = useState("");
  const [newKind, setNewKind] = useState("string");
  const [error, setError] = useState("");

  function addKey() {
    const key = newKey.trim();
    if (!key) {
      setError("Key name cannot be empty.");
      return;
    }
    if (Object.hasOwn(value, key)) {
      setError(`Key "${key}" already exists.`);
      return;
    }
    setError("");
    setNewKey("");
    onRootChange(setAtPath(rootValue, [...path, key], defaultJsonValue(newKind)));
  }

  function renameKey(oldKey: string) {
    const nextKey = window.prompt("Rename JSON key", oldKey);
    if (nextKey === null || nextKey === oldKey) return;
    const result = renameKeyAtPath(rootValue, path, oldKey, nextKey);
    if (!result.ok) {
      setError(result.error);
      return;
    }
    setError("");
    onRootChange(result.value);
  }

  function deleteKey(key: string) {
    if (!window.confirm(`Delete key "${key}"?`)) return;
    onRootChange(deleteAtPath(rootValue, [...path, key]));
  }

  return (
    <div className="json-object-editor">
      {Object.entries(value).map(([key, entryValue]) => (
        <div className="json-object-row" key={key}>
          <JsonTreeNode
            rootValue={rootValue}
            inheritedRoot={inheritedRoot}
            path={[...path, key]}
            label={key}
            value={entryValue}
            hints={hints}
            restoreStrategy={restoreStrategy}
            allowObjectStructuralEdits={allowObjectStructuralEdits}
            allowArrayStructuralEdits={allowArrayStructuralEdits}
            groupContext={groupContext}
            onRootChange={onRootChange}
          />
          {allowObjectStructuralEdits ? (
            <div className="json-row-actions">
              <button
                type="button"
                className="json-action-button"
                onClick={() => renameKey(key)}
              >
                Rename
              </button>
              <button
                type="button"
                className="json-action-button"
                onClick={() => deleteKey(key)}
              >
                Delete
              </button>
            </div>
          ) : null}
        </div>
      ))}
      {allowObjectStructuralEdits ? (
        <div className="json-add-row">
          <input
            className="json-add-input"
            aria-label="New object key"
            placeholder="newKey"
            value={newKey}
            onChange={(event) => setNewKey(event.target.value)}
          />
          <select
            className="json-add-select"
            aria-label="New key value type"
            value={newKind}
            onChange={(event) => setNewKind(event.target.value)}
          >
            <option value="string">string</option>
            <option value="number">number</option>
            <option value="boolean">boolean</option>
            <option value="object">object</option>
            <option value="array">array</option>
            <option value="null">null</option>
          </select>
          <button type="button" className="json-add-button" onClick={addKey}>
            Add key
          </button>
        </div>
      ) : null}
      {error ? <strong className="json-editor-error">{error}</strong> : null}
    </div>
  );
}
