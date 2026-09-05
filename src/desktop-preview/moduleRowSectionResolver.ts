import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { requiredRecord, requiredString } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ModuleRow } from "./moduleRowSectionContract.js";

export function requiredRuntimeRows(preview: Record<string, unknown>, key: string, idPrefix: string, ownerId: string, rowCount = 2): Record<string, unknown>[] {
  return requiredIdentifiedRows(preview[key], key, idPrefix, ownerId, rowCount);
}

export function requiredRows(owner: Record<string, unknown>, key: string, idPrefix: string, ownerId: string, rowCount = 2): Record<string, unknown>[] {
  return requiredIdentifiedRows(owner[key], key, idPrefix, ownerId, rowCount);
}

export function resolveRow<TContent>(
  payload: DesignPreviewPayload,
  ownerId: string,
  section: string,
  row: number,
  rowConfig: Record<string, unknown>,
  runtimeRow: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  componentType: string,
  resolve: (payload: DesignPreviewPayload) => TContent,
): ModuleRow<TContent> {
  const id = requiredString(rowConfig, "id", `${ownerId}.${section}.row${row}.id`);
  const rowSlot = requiredRecord(rowConfig, "rowSlot", `${ownerId}.${section}.${id}.rowSlot`);
  const config = embeddedComponentConfig(componentBaseConfigs, rowSlot, componentType, `${ownerId}.${section}.${id}.rowSlot`);
  return {
    id,
    content: resolve({
      ...payload,
      componentType,
      configJson: JSON.stringify(config),
      designPreviewJson: JSON.stringify({ ...runtimeRow, viewportSize: "390|80" }),
    }),
  };
}

function requiredIdentifiedRows(value: unknown, key: string, idPrefix: string, ownerId: string, rowCount: number) {
  if (!Array.isArray(value) || value.length !== rowCount) throw new Error(`${ownerId} collection '${key}' must contain exactly ${rowCount} '${idPrefix}' rows`);
  return value.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) throw new Error(`${ownerId}.${key}[${index}] must be an object`);
    const row = item as Record<string, unknown>;
    const expectedId = `${idPrefix}${index + 1}`;
    if (requiredString(row, "id", `${ownerId}.${key}[${index}].id`) !== expectedId) throw new Error(`${ownerId}.${key}[${index}] must have id '${expectedId}'`);
    return row;
  });
}
