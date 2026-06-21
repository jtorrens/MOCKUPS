import { useMemo, useState } from "react";
import type { AppFieldDefinition, AppTableDefinition } from "../../api/client.js";
import { JsonTreeNode } from "./JsonTreeNode.js";
import { RawJsonEditor } from "./RawJsonEditor.js";
import { buildJsonUiHints } from "./uiHints.js";
import {
  parseJsonObject,
  stringifyJson,
  type JsonValue,
} from "./jsonEditorUtils.js";

interface JsonTreeEditorProps {
  table: AppTableDefinition;
  field: AppFieldDefinition;
  record?: Record<string, unknown>;
  rawText: string;
  disabled?: boolean;
  testId?: string;
  inheritedValue?: Record<string, unknown>;
  restoreStrategy?: "remove" | "set";
  allowObjectStructuralEdits?: boolean;
  allowArrayStructuralEdits?: boolean;
  onRawTextChange: (nextRawText: string) => void;
}

export function JsonTreeEditor({
  table,
  field,
  record,
  rawText,
  disabled,
  testId,
  inheritedValue,
  restoreStrategy = "remove",
  allowObjectStructuralEdits = false,
  allowArrayStructuralEdits = false,
  onRawTextChange,
}: JsonTreeEditorProps) {
  const [mode, setMode] = useState<"tree" | "raw">("tree");
  const parsed = useMemo(() => parseJsonObject(rawText), [rawText]);
  const hints = useMemo(
    () => buildJsonUiHints(table, field, record),
    [field, record, table],
  );

  function setTreeValue(nextValue: JsonValue) {
    onRawTextChange(stringifyJson(nextValue));
  }

  function switchToTree() {
    if (!parsed.ok) return;
    setMode("tree");
  }

  const hintCount = Object.keys(hints).length;

  return (
    <div
      className={`json-tree-editor mode-${mode}`}
      data-testid={mode === "tree" ? testId : undefined}
    >
      <div className="json-editor-toolbar">
        <div>
          <button
            type="button"
            className={mode === "tree" ? "active" : ""}
            disabled={!parsed.ok}
            onClick={switchToTree}
          >
            Tree
          </button>
          <button
            type="button"
            className={mode === "raw" ? "active" : ""}
            onClick={() => setMode("raw")}
          >
            Raw JSON
          </button>
        </div>
        <span>
          {hintCount} hints · {parsed.ok ? "valid JSON object" : "invalid raw JSON"}
        </span>
      </div>

      {mode === "raw" || !parsed.ok ? (
        <RawJsonEditor
          rawText={rawText}
          testId={mode === "raw" ? testId : undefined}
          disabled={disabled}
          error={parsed.ok ? undefined : parsed.error}
          onChange={onRawTextChange}
        />
      ) : (
        <div className="json-tree" aria-label={`${field.label} tree editor`}>
          <JsonTreeNode
            rootValue={parsed.value}
            inheritedRoot={inheritedValue as JsonValue | undefined}
            path={[]}
            label={field.label}
            value={parsed.value}
            hints={hints}
            restoreStrategy={restoreStrategy}
            allowObjectStructuralEdits={allowObjectStructuralEdits}
            allowArrayStructuralEdits={allowArrayStructuralEdits}
            onRootChange={setTreeValue}
          />
        </div>
      )}
    </div>
  );
}
