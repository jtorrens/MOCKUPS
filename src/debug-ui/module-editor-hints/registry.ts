import { coreChatV1EditorHints } from "./coreChatV1.js";
import type {
  ModuleEditorHintContract,
  ModuleEditorHintField,
} from "./types.js";

const MODULE_EDITOR_HINTS = [coreChatV1EditorHints] satisfies ModuleEditorHintContract[];

function moduleContextFromRecord(
  tableId: string,
  record: Record<string, unknown> | undefined,
): { moduleId: string; schemaVersion: number } | undefined {
  if (!record) return undefined;
  if (
    tableId !== "screen_instances" &&
    tableId !== "module_instances" &&
    tableId !== "module_theme_configs"
  ) {
    return undefined;
  }
  const moduleId = record.module_id;
  const schemaVersion = record.module_schema_version;
  if (typeof moduleId !== "string" || typeof schemaVersion !== "number") {
    return undefined;
  }
  return { moduleId, schemaVersion };
}

function moduleFieldForJsonField(
  tableId: string,
  fieldColumn: string,
): ModuleEditorHintField | undefined {
  if (
    tableId === "screen_instances" &&
    (fieldColumn === "module_data_json" ||
      fieldColumn === "module_config_json" ||
      fieldColumn === "module_tokens_override_json")
  ) {
    return fieldColumn;
  }
  if (
    tableId === "module_instances" &&
    (fieldColumn === "content_json" || fieldColumn === "behavior_json")
  ) {
    return fieldColumn;
  }
  if (
    tableId === "module_theme_configs" &&
    (fieldColumn === "tokens_json" || fieldColumn === "metadata_json")
  ) {
    return fieldColumn;
  }
  return undefined;
}

export function moduleJsonUiHintsForRecord(
  tableId: string,
  fieldColumn: string,
  record: Record<string, unknown> | undefined,
) {
  const context = moduleContextFromRecord(tableId, record);
  const moduleField = moduleFieldForJsonField(tableId, fieldColumn);
  if (!context || !moduleField) return {};

  const contract = MODULE_EDITOR_HINTS.find(
    (candidate) =>
      candidate.moduleId === context.moduleId &&
      candidate.schemaVersion === context.schemaVersion,
  );
  return contract?.fields[moduleField] ?? {};
}
