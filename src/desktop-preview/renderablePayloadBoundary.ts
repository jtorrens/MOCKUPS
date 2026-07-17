import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { applyRuntimeInputForwarding } from "./runtimeInputForwarding.js";

export function resolveRenderablePayload(
  payload: DesignPreviewPayload,
): DesignPreviewPayload {
  requireObject(payload.runtimeContractJson, "runtime contract");
  return applyRuntimeInputForwarding(payload);
}

function requireObject(json: string, label: string) {
  const value: unknown = JSON.parse(json);
  if (typeof value !== "object" || value === null || Array.isArray(value)) {
    throw new Error(`Invalid ${label}`);
  }
}
