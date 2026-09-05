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
  const config = rebaseNestedStableIds(
    embeddedComponentConfig(componentBaseConfigs, rowSlot, componentType, `${ownerId}.${section}.${id}.rowSlot`),
    id,
  );
  const runtime = rebaseNestedStableIds(runtimeRow, id);
  return {
    id,
    content: resolve({
      ...payload,
      componentType,
      configJson: JSON.stringify(config),
      designPreviewJson: JSON.stringify({ slotInputs: runtime.slotInputs, viewportSize: "390|80" }),
    }),
  };
}

function rebaseNestedStableIds(value: Record<string, unknown>, ownerId: string): Record<string, unknown> {
  const rebased = structuredClone(value);
  visit(rebased, ownerId);
  return rebased;

  function visit(current: unknown, scopeId: string): void {
    if (Array.isArray(current)) {
      for (const item of current) {
        let itemScopeId = scopeId;
        if (item && typeof item === "object" && !Array.isArray(item)) {
          const record = item as Record<string, unknown>;
          const originalId = record.id;
          if (typeof originalId === "string") {
            const rebasedId = originalId.startsWith(`${scopeId}_`)
              ? originalId
              : `${scopeId}_${originalId}`;
            record.id = rebasedId;
            itemScopeId = rebasedId;
          }
        }
        visit(item, itemScopeId);
      }
      return;
    }
    if (!current || typeof current !== "object") return;
    for (const child of Object.values(current as Record<string, unknown>)) visit(child, scopeId);
  }
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
