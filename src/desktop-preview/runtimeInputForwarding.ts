import type { DesignPreviewPayload } from "./designPreviewPayload.js";

const storageKey = "$forwardedInputs";
const runtimeFieldIdsKey = "__runtimeFieldIds";

type JsonRecord = Record<string, unknown>;

export function applyRuntimeInputForwarding(
  payload: DesignPreviewPayload,
): DesignPreviewPayload {
  const config = parseRecord(payload.configJson, "component config");
  const runtime = parseRecord(payload.designPreviewJson, "component runtime payload");
  apply(config, runtime, "");
  return {
    ...payload,
    configJson: JSON.stringify(config),
  };
}

function apply(node: unknown, runtime: JsonRecord, inheritedOwnerId: string) {
  if (Array.isArray(node)) {
    node.forEach((child) => apply(child, runtime, inheritedOwnerId));
    return;
  }
  if (!isRecord(node)) return;

  const ownerId = typeof node.id === "string" && node.id ? node.id : inheritedOwnerId;

  const forwarding = node[storageKey];
  if (isRecord(forwarding)) {
    const entries = Object.entries(forwarding).map(([targetKey, rawDefinition]) => {
      if (!isRecord(rawDefinition) || typeof rawDefinition.jsonKey !== "string") {
        throw new Error(`Invalid forwarded runtime input definition ${targetKey}`);
      }
      const runtimeOwner = ownerId
        ? findRuntimeOwner(runtime, ownerId, rawDefinition.jsonKey)
          ?? (Object.hasOwn(runtime, rawDefinition.jsonKey) ? runtime : undefined)
        : runtime;
      return { targetKey, rawDefinition, runtimeOwner };
    });
    const ownedEntries = entries.filter(({ rawDefinition, runtimeOwner }) =>
      !!runtimeOwner && Object.hasOwn(runtimeOwner, rawDefinition.jsonKey as string));
    if (ownedEntries.length > 0 && ownedEntries.length !== entries.length) {
      const missing = entries.find(({ rawDefinition, runtimeOwner }) =>
        !runtimeOwner || !Object.hasOwn(runtimeOwner, rawDefinition.jsonKey as string))!;
      throw new Error(`Missing forwarded runtime value ${missing.rawDefinition.jsonKey}`);
    }
    for (const { targetKey, rawDefinition, runtimeOwner } of ownedEntries) {
      if (!runtimeOwner) {
        throw new Error(`Missing forwarded runtime value ${rawDefinition.jsonKey}`);
      }
      if (typeof rawDefinition.id !== "string" || !rawDefinition.id) {
        throw new Error(`Forwarded runtime input '${targetKey}' has no stable field id`);
      }
      const runtimeFieldIds = isRecord(node[runtimeFieldIdsKey])
        ? node[runtimeFieldIdsKey] as JsonRecord
        : {};
      runtimeFieldIds[targetKey] = rawDefinition.id;
      node[runtimeFieldIdsKey] = runtimeFieldIds;
      node[targetKey] = runtimeOwner[rawDefinition.jsonKey as string];
      if (
        typeof rawDefinition.resolvedJsonKey === "string" &&
        rawDefinition.resolvedJsonKey &&
        typeof rawDefinition.targetResolvedJsonKey === "string" &&
        rawDefinition.targetResolvedJsonKey &&
        Object.hasOwn(runtimeOwner, rawDefinition.resolvedJsonKey)
      ) {
        node[rawDefinition.targetResolvedJsonKey] = runtimeOwner[rawDefinition.resolvedJsonKey];
      }
    }
    if (ownedEntries.length > 0) delete node[storageKey];
  }

  for (const [key, child] of Object.entries(node)) {
    if (key !== storageKey) apply(child, runtime, ownerId);
  }
}

function findRuntimeOwner(node: unknown, id: string, jsonKey: string): JsonRecord | undefined {
  if (Array.isArray(node)) {
    for (const child of node) {
      const found = findRuntimeOwner(child, id, jsonKey);
      if (found) return found;
    }
    return undefined;
  }
  if (!isRecord(node)) return undefined;
  if (node.id === id) {
    if (Object.hasOwn(node, jsonKey)) return node;
    for (const child of Object.values(node)) {
      if (isRecord(child) && Object.hasOwn(child, jsonKey)) return child;
    }
  }
  for (const child of Object.values(node)) {
    const found = findRuntimeOwner(child, id, jsonKey);
    if (found) return found;
  }
  return undefined;
}

function parseRecord(json: string, label: string): JsonRecord {
  const value: unknown = JSON.parse(json);
  if (!isRecord(value)) throw new Error(`Invalid ${label}`);
  return value;
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
