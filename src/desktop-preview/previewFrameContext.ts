import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { isRecord, parseObject } from "./previewJsonHelpers.js";

export function currentBoundaryLocalFrame(payload: DesignPreviewPayload) {
  return requiredFrame(payload.localFrame, "Preview payload localFrame");
}

export function rootScreenFrame(payload: DesignPreviewPayload) {
  const instance = parseObject(payload.instanceJson, "Preview instance envelope");
  if (!Object.hasOwn(instance, "context")) {
    if (payload.kind === "moduleInstance") {
      throw new Error("Module Instance Preview requires its exact Screen context");
    }
    return currentBoundaryLocalFrame(payload);
  }

  const context = instance.context;
  if (!isRecord(context)) {
    throw new Error("Preview instance context must be an object");
  }
  if (Object.hasOwn(context, "localFrame")) {
    throw new Error("Preview instance context localFrame is retired; use screenFrame");
  }
  return requiredFrame(context.screenFrame, "Preview instance context screenFrame");
}

export function validateRootFrameIdentity(payload: DesignPreviewPayload) {
  const localFrame = currentBoundaryLocalFrame(payload);
  const screenFrame = rootScreenFrame(payload);
  if (payload.kind === "moduleInstance" && localFrame !== screenFrame) {
    throw new Error(
      `Root Module Instance Preview localFrame ${localFrame} does not match screenFrame ${screenFrame}`,
    );
  }
}

function requiredFrame(value: unknown, path: string) {
  if (typeof value === "number" && Number.isInteger(value) && value >= 0) return value;
  throw new Error(`${path} must be a non-negative integer`);
}
