import { embeddedComponentConfig } from "./runtimePreviewDocumentContract.js";
import { requiredBoolean, requiredRecord, requiredString } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ModuleRow } from "./moduleRowSectionContract.js";

export function requiredRuntimeRows(preview: Record<string, unknown>, key: string, ownerId: string, rowCount = 2): Record<string, unknown>[] {
  return requiredIdentifiedRows(preview[key], key, ownerId, rowCount);
}

export function requiredRows(owner: Record<string, unknown>, key: string, ownerId: string, rowCount = 2): Record<string, unknown>[] {
  return requiredIdentifiedRows(owner[key], key, ownerId, rowCount);
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
  if (requiredString(runtimeRow, "id", `${ownerId}.${section}.runtime.id`) !== id) {
    throw new Error(`${ownerId}.${section} runtime must retain the exact ordered row identities`);
  }
  const config = embeddedComponentConfig(componentBaseConfigs, rowSlot, componentType, `${ownerId}.${section}.${id}.rowSlot`);
  return {
    id,
    visible: requiredBoolean(rowConfig, "visible", `${ownerId}.${section}.${id}.visible`),
    content: resolve({
      ...payload,
      componentType,
      configJson: JSON.stringify(config),
      designPreviewJson: JSON.stringify({ slotInputs: runtimeRow.slotInputs, viewportSize: "390|80" }),
    }),
  };
}

function requiredIdentifiedRows(value: unknown, key: string, ownerId: string, rowCount: number) {
  if (!Array.isArray(value) || value.length !== rowCount) throw new Error(`${ownerId} collection '${key}' must contain exactly ${rowCount} rows`);
  const ids = new Set<string>();
  return value.map((item, index) => {
    if (!item || typeof item !== "object" || Array.isArray(item)) throw new Error(`${ownerId}.${key}[${index}] must be an object`);
    const row = item as Record<string, unknown>;
    const id = requiredString(row, "id", `${ownerId}.${key}[${index}].id`);
    if (ids.has(id)) throw new Error(`${ownerId}.${key} contains duplicate stable id '${id}'`);
    ids.add(id);
    return row;
  });
}
