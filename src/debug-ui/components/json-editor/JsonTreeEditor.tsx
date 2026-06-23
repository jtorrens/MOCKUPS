import { useMemo } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../../api/client.js";
import { JsonTreeNode } from "./JsonTreeNode.js";
import { JsonArrayEditor } from "./JsonArrayEditor.js";
import { JsonObjectEditor } from "./JsonObjectEditor.js";
import { RawJsonEditor } from "./RawJsonEditor.js";
import { TokenOverrideEditor } from "./TokenOverrideEditor.js";
import { buildJsonUiHints } from "./uiHints.js";
import {
  isJsonObject,
  parseJsonObject,
  parseJsonValue,
  stringifyJson,
  type JsonValue,
} from "./jsonEditorUtils.js";

interface JsonTreeEditorProps {
  table: AppTableDefinition;
  field: AppFieldDefinition;
  record?: Record<string, unknown>;
  records?: Record<string, AppRecord[]>;
  rawText: string;
  disabled?: boolean;
  testId?: string;
  inheritedValue?: Record<string, unknown>;
  restoreStrategy?: "remove" | "set";
  groupContext?: string;
  allowObjectStructuralEdits?: boolean;
  allowArrayStructuralEdits?: boolean;
  onRawTextChange: (nextRawText: string) => void;
}

export function JsonTreeEditor({
  table,
  field,
  record,
  records = {},
  rawText,
  disabled,
  testId,
  inheritedValue,
  restoreStrategy = "remove",
  groupContext,
  allowObjectStructuralEdits = false,
  allowArrayStructuralEdits = false,
  onRawTextChange,
}: JsonTreeEditorProps) {
  const parsed = useMemo(
    () => (groupContext ? parseJsonValue(rawText) : parseJsonObject(rawText)),
    [groupContext, rawText],
  );
  const hints = useMemo(
    () => buildJsonUiHints(table, field, record),
    [field, record, table],
  );

  function setTreeValue(nextValue: JsonValue) {
    onRawTextChange(stringifyJson(nextValue));
  }

  const canUseTokenOverrideEditor =
    table.id === "screen_instances" &&
    field.column === "module_tokens_override_json" &&
    parsed.ok &&
    isJsonObject(inheritedValue as JsonValue);
  const canUseInheritedOverrideEditor =
    table.id === "screen_instances" &&
    (field.column === "module_config_json" ||
      field.column === "transform_json") &&
    parsed.ok &&
    isJsonObject(inheritedValue as JsonValue);
  const canUseModuleThemeTokenEditor =
    table.id === "module_theme_configs" &&
    field.column === "tokens_json" &&
    parsed.ok &&
    isJsonObject(inheritedValue as JsonValue);
  const canUseAppInheritedTokenEditor =
    table.id === "apps" &&
    field.column === "config_json" &&
    parsed.ok &&
    isJsonObject(inheritedValue as JsonValue);
  return (
    <div
      className={`json-tree-editor ${parsed.ok ? "mode-tree" : "mode-raw"} ${
        groupContext ? `json-context-${groupContext}` : ""
      }`}
      data-testid={parsed.ok ? testId : undefined}
    >
      {!parsed.ok ? (
        <RawJsonEditor
          rawText={rawText}
          testId={testId}
          disabled={disabled}
          error={parsed.error}
          onChange={onRawTextChange}
        />
      ) : canUseTokenOverrideEditor ? (
        <TokenOverrideEditor
          rootValue={parsed.value}
          inheritedRoot={inheritedValue as JsonValue}
          hints={hints}
          groupContext={groupContext}
          onRootChange={setTreeValue}
        />
      ) : canUseInheritedOverrideEditor ? (
        <TokenOverrideEditor
          rootValue={parsed.value}
          inheritedRoot={inheritedValue as JsonValue}
          hints={hints}
          inheritedColumnLabel="Inherited from template"
          groupContext={groupContext}
          restoreMode={restoreStrategy}
          onRootChange={setTreeValue}
        />
      ) : canUseModuleThemeTokenEditor ? (
        <TokenOverrideEditor
          rootValue={parsed.value}
          inheritedRoot={inheritedValue as JsonValue}
          hints={hints}
          inheritedColumnLabel="Inherited"
          groupContext={groupContext}
          restoreMode={restoreStrategy}
          onRootChange={setTreeValue}
        />
      ) : canUseAppInheritedTokenEditor ? (
        <TokenOverrideEditor
          rootValue={parsed.value}
          inheritedRoot={inheritedValue as JsonValue}
          hints={hints}
          inheritedColumnLabel="Inherited from theme"
          groupContext={groupContext}
          restoreMode={restoreStrategy}
          onRootChange={setTreeValue}
        />
      ) : groupContext && Array.isArray(parsed.value) ? (
        <div
          className="json-tree json-tree-inline-root json-tree-array-root"
          aria-label={`${field.label} tree editor`}
        >
          <JsonArrayEditor
            rootValue={parsed.value}
            inheritedRoot={inheritedValue as JsonValue | undefined}
            path={[]}
            value={parsed.value}
            hints={hints}
            restoreStrategy={restoreStrategy}
            allowObjectStructuralEdits={allowObjectStructuralEdits}
            allowArrayStructuralEdits={allowArrayStructuralEdits}
            groupContext={groupContext}
            onRootChange={setTreeValue}
          />
        </div>
      ) : groupContext && isJsonObject(parsed.value) ? (
        <div className="json-tree json-tree-inline-root" aria-label={`${field.label} tree editor`}>
          <JsonObjectEditor
            rootValue={parsed.value}
            inheritedRoot={inheritedValue as JsonValue | undefined}
            path={[]}
            value={parsed.value}
            hints={hints}
            restoreStrategy={restoreStrategy}
            allowObjectStructuralEdits={allowObjectStructuralEdits}
            allowArrayStructuralEdits={allowArrayStructuralEdits}
            groupContext={groupContext}
            onRootChange={setTreeValue}
          />
        </div>
      ) : (
        <div className="json-tree" aria-label={`${field.label} tree editor`}>
          <JsonTreeNode
            rootValue={parsed.value}
            inheritedRoot={inheritedValue as JsonValue | undefined}
            path={[]}
            label={groupContext ?? field.label}
            value={parsed.value}
            hints={hints}
            restoreStrategy={restoreStrategy}
            allowObjectStructuralEdits={allowObjectStructuralEdits}
            allowArrayStructuralEdits={allowArrayStructuralEdits}
            groupContext={groupContext}
            onRootChange={setTreeValue}
          />
        </div>
      )}
    </div>
  );
}
