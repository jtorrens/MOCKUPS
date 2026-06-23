import type { JsonUiHints } from "../components/json-editor/uiHints.js";
import { appConfigDescriptors, appMetadataDescriptors } from "./appDescriptors.js";
import { descriptorsToHints } from "./common.js";
import {
  coreChatV1BehaviorDescriptors,
  coreChatV1ContentDescriptors,
  coreChatV1TokenDescriptors,
} from "./coreChatV1Descriptors.js";
import {
  appRecordDescriptors,
  moduleInstanceRecordDescriptors,
  moduleThemeConfigRecordDescriptors,
  screenInstanceRecordDescriptors,
} from "./recordDescriptors.js";
import { themeTokenDescriptors } from "./themeDescriptors.js";
import type { FieldDescriptor, FieldDescriptorContext } from "./types.js";

function moduleContext(
  record: Record<string, unknown> | undefined,
): { moduleId: string; schemaVersion: number } | undefined {
  const moduleId = record?.module_id;
  const schemaVersion = record?.module_schema_version;
  return typeof moduleId === "string" && typeof schemaVersion === "number"
    ? { moduleId, schemaVersion }
    : undefined;
}

export function fieldDescriptorsForContext({
  tableId,
  fieldColumn,
  record,
}: FieldDescriptorContext): FieldDescriptor[] {
  if (fieldColumn !== "") {
    const scalarDescriptors = recordFieldDescriptorsForTable(tableId).filter(
      (descriptor) => descriptor.storagePath[0] === fieldColumn,
    );
    if (scalarDescriptors.length > 0) {
      return scalarDescriptors;
    }
  }
  if (tableId === "apps" && fieldColumn === "config_json") {
    return appConfigDescriptors;
  }
  if (tableId === "apps" && fieldColumn === "metadata_json") {
    return appMetadataDescriptors;
  }
  if (tableId === "themes" && fieldColumn === "tokens_json") {
    return themeTokenDescriptors;
  }
  if (tableId === "module_theme_configs" && fieldColumn === "tokens_json") {
    const context = moduleContext(record);
    if (context?.moduleId === "core.chat" && context.schemaVersion === 1) {
      return coreChatV1TokenDescriptors;
    }
  }
  if (tableId === "module_instances") {
    const context = moduleContext(record);
    if (context?.moduleId === "core.chat" && context.schemaVersion === 1) {
      if (fieldColumn === "content_json") return coreChatV1ContentDescriptors;
      if (fieldColumn === "behavior_json") return coreChatV1BehaviorDescriptors;
    }
  }
  return [];
}

export function recordFieldDescriptorsForTable(tableId: string): FieldDescriptor[] {
  if (tableId === "apps") return appRecordDescriptors;
  if (tableId === "module_theme_configs") {
    return moduleThemeConfigRecordDescriptors;
  }
  if (tableId === "module_instances") return moduleInstanceRecordDescriptors;
  if (tableId === "screen_instances") return screenInstanceRecordDescriptors;
  return [];
}

export function fieldDescriptorHintsForContext(
  context: FieldDescriptorContext,
): JsonUiHints {
  return descriptorsToHints(fieldDescriptorsForContext(context)) as JsonUiHints;
}
