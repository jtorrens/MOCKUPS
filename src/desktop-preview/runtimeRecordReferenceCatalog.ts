import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { parseObject, requiredRecord } from "./componentResolverCommon.js";
import type { JsonRecord } from "./previewJsonHelpers.js";

export function resolvedRuntimeRecordReference(
  payload: DesignPreviewPayload,
  tableId: string,
  recordId: string,
  owner: string,
): JsonRecord {
  const catalog = parseObject(payload.runtimeRecordReferencesJson ?? "{}");
  const table = requiredRecord(catalog, tableId, `${owner} Runtime record catalog`);
  return requiredRecord(table, recordId, `${owner} Runtime record catalog.${tableId}`);
}
