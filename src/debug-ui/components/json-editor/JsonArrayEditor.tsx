import { useState } from "react";
import type { JsonUiHints } from "./uiHints.js";
import {
  cloneJson,
  defaultJsonValue,
  setAtPath,
  type JsonPath,
  type JsonValue,
} from "./jsonEditorUtils.js";
import { JsonTreeNode } from "./JsonTreeNode.js";
import type { ProductionFontCatalog } from "./productionFonts.js";

interface JsonArrayEditorProps {
  rootValue: JsonValue;
  inheritedRoot?: JsonValue;
  path: JsonPath;
  value: JsonValue[];
  hints: JsonUiHints;
  restoreStrategy: "remove" | "set";
  allowObjectStructuralEdits: boolean;
  allowArrayStructuralEdits: boolean;
  groupContext?: string;
  productionFontCatalog?: ProductionFontCatalog;
  onRootChange: (nextValue: JsonValue) => void;
}

export function JsonArrayEditor({
  rootValue,
  inheritedRoot,
  path,
  value,
  hints,
  restoreStrategy,
  allowObjectStructuralEdits,
  allowArrayStructuralEdits,
  groupContext,
  productionFontCatalog,
  onRootChange,
}: JsonArrayEditorProps) {
  const [newKind, setNewKind] = useState("object");

  function replaceArray(nextArray: JsonValue[]) {
    onRootChange(setAtPath(rootValue, path, nextArray));
  }

  function addItem() {
    replaceArray([...value, defaultJsonValue(newKind)]);
  }

  function duplicateItem(index: number) {
    replaceArray([
      ...value.slice(0, index + 1),
      cloneJson(value[index]),
      ...value.slice(index + 1),
    ]);
  }

  function deleteItem(index: number) {
    if (!window.confirm(`Delete array item [${index}]?`)) return;
    replaceArray(value.filter((_, candidateIndex) => candidateIndex !== index));
  }

  function moveItem(index: number, direction: -1 | 1) {
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= value.length) return;
    const nextArray = [...value];
    const current = nextArray[index];
    nextArray[index] = nextArray[targetIndex];
    nextArray[targetIndex] = current;
    replaceArray(nextArray);
  }

  return (
    <div className="json-array-editor">
      {value.map((entryValue, index) => (
        <div className="json-array-row" key={index}>
          <JsonTreeNode
            rootValue={rootValue}
            inheritedRoot={inheritedRoot}
            path={[...path, index]}
            label={`[${index}]`}
            value={entryValue}
            hints={hints}
            restoreStrategy={restoreStrategy}
            allowObjectStructuralEdits={allowObjectStructuralEdits}
            allowArrayStructuralEdits={allowArrayStructuralEdits}
            groupContext={groupContext}
            productionFontCatalog={productionFontCatalog}
            onRootChange={onRootChange}
          />
          {allowArrayStructuralEdits ? (
            <div className="json-row-actions">
              <button
                type="button"
                className="json-action-button"
                aria-label={`Move item ${index + 1} up`}
                title="Move up"
                disabled={index === 0}
                onClick={() => moveItem(index, -1)}
              >
                ↑
              </button>
              <button
                type="button"
                className="json-action-button"
                aria-label={`Move item ${index + 1} down`}
                title="Move down"
                disabled={index === value.length - 1}
                onClick={() => moveItem(index, 1)}
              >
                ↓
              </button>
              <button
                type="button"
                className="json-action-button"
                onClick={() => duplicateItem(index)}
              >
                Duplicate
              </button>
              <button
                type="button"
                className="json-action-button"
                onClick={() => deleteItem(index)}
              >
                Delete
              </button>
            </div>
          ) : null}
        </div>
      ))}
      {allowArrayStructuralEdits ? (
        <div className="json-add-row">
          <select
            className="json-add-select"
            aria-label="New array item type"
            value={newKind}
            onChange={(event) => setNewKind(event.target.value)}
          >
            <option value="object">object</option>
            <option value="string">string</option>
            <option value="number">number</option>
            <option value="boolean">boolean</option>
            <option value="array">array</option>
            <option value="null">null</option>
          </select>
          <button type="button" className="json-add-button" onClick={addItem}>
            Add item
          </button>
        </div>
      ) : null}
    </div>
  );
}
