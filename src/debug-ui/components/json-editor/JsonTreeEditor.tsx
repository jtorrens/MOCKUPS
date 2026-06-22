import { useMemo } from "react";
import type {
  AppFieldDefinition,
  AppRecord,
  AppTableDefinition,
} from "../../api/client.js";
import { JsonTreeNode } from "./JsonTreeNode.js";
import { RawJsonEditor } from "./RawJsonEditor.js";
import { ScreenTemplateConfigEditor } from "./ScreenTemplateConfigEditor.js";
import { TokenOverrideEditor } from "./TokenOverrideEditor.js";
import { buildJsonUiHints } from "./uiHints.js";
import {
  isJsonObject,
  parseJsonObject,
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
  screenTemplateSection?: "behavior" | "overrides";
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
  screenTemplateSection,
  groupContext,
  allowObjectStructuralEdits = false,
  allowArrayStructuralEdits = false,
  onRawTextChange,
}: JsonTreeEditorProps) {
  const parsed = useMemo(() => parseJsonObject(rawText), [rawText]);
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
  const canUseScreenTemplateConfigEditor =
    table.id === "screen_templates" &&
    field.column === "config_json" &&
    parsed.ok;

  return (
    <div
      className={`json-tree-editor ${parsed.ok ? "mode-tree" : "mode-raw"}`}
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
      ) : canUseScreenTemplateConfigEditor ? (
        <ScreenTemplateConfigEditor
          rootValue={parsed.value}
          record={record}
          records={records}
          section={screenTemplateSection}
          onRootChange={setTreeValue}
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
            groupContext={groupContext}
            onRootChange={setTreeValue}
          />
        </div>
      )}
    </div>
  );
}
