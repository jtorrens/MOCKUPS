import type { DesignPreviewPayload } from "./designPreviewPayload.js";

const storageKey = "$forwardedInputs";

type JsonRecord = Record<string, unknown>;

export function applyRuntimeInputForwarding(
  payload: DesignPreviewPayload,
): DesignPreviewPayload {
  const config = parseRecord(payload.configJson, "component config");
  const runtime = parseRecord(payload.designPreviewJson ?? "{}", "component runtime payload");
  apply(config, runtime);
  return {
    ...payload,
    configJson: JSON.stringify(config),
  };
}

function apply(node: unknown, runtime: JsonRecord) {
  if (Array.isArray(node)) {
    node.forEach((child) => apply(child, runtime));
    return;
  }
  if (!isRecord(node)) return;

  const forwarding = node[storageKey];
  if (isRecord(forwarding)) {
    for (const [targetKey, rawDefinition] of Object.entries(forwarding)) {
      if (!isRecord(rawDefinition) || typeof rawDefinition.jsonKey !== "string") {
        throw new Error(`Invalid forwarded runtime input definition ${targetKey}`);
      }
      if (!Object.hasOwn(runtime, rawDefinition.jsonKey)) {
        throw new Error(`Missing forwarded runtime value ${rawDefinition.jsonKey}`);
      }
      node[targetKey] = runtime[rawDefinition.jsonKey];
      if (
        typeof rawDefinition.resolvedJsonKey === "string" &&
        rawDefinition.resolvedJsonKey &&
        typeof rawDefinition.targetResolvedJsonKey === "string" &&
        rawDefinition.targetResolvedJsonKey &&
        Object.hasOwn(runtime, rawDefinition.resolvedJsonKey)
      ) {
        node[rawDefinition.targetResolvedJsonKey] = runtime[rawDefinition.resolvedJsonKey];
      }
    }
    delete node[storageKey];
  }

  for (const [key, child] of Object.entries(node)) {
    if (key !== storageKey) apply(child, runtime);
  }
}

function parseRecord(json: string, label: string): JsonRecord {
  const value: unknown = JSON.parse(json);
  if (!isRecord(value)) throw new Error(`Invalid ${label}`);
  return value;
}

function isRecord(value: unknown): value is JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
