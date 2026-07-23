import { parseObject, requiredNumber, requiredNumberPair, requiredRecord, requiredString } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { FingerprintDesignContract, FingerprintState } from "./fingerprintComponentContract.js";

export function resolveFingerprintComponent(payload: DesignPreviewPayload): FingerprintDesignContract {
  return resolveFingerprintComponentFromRecords(
    parseObject(payload.configJson),
    parseObject(payload.designPreviewJson),
    "component.fingerprint",
  );
}

export function resolveFingerprintComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  id: string,
): FingerprintDesignContract {
  const fingerprint = requiredRecord(config, "fingerprint", `${id}.fingerprint`);
  const size = requiredNumberPair(fingerprint, "size", `${id}.size`);
  const state = recognitionState(requiredString(inputs, "state", `${id}.runtime.state`));
  const progress = requiredNumber(inputs, "progress", `${id}.runtime.progress`);
  if (size.first <= 0 || size.second <= 0) throw new Error(`${id}.size must be positive`);
  if (progress < 0 || progress > 1) throw new Error(`${id}.runtime.progress must be between 0 and 1`);
  const states = requiredRecord(fingerprint, "states", `${id}.states`);
  const stateConfig = requiredRecord(states, state, `${id}.states.${state}`);
  const scanLineThickness = requiredNumber(fingerprint, "scanLineThickness", `${id}.scanLineThickness`);
  const iconSizeMultiplier = requiredNumber(fingerprint, "iconSizeMultiplier", `${id}.iconSizeMultiplier`);
  if (scanLineThickness <= 0 || iconSizeMultiplier <= 0) throw new Error(`${id} scanLineThickness and iconSizeMultiplier must be positive`);
  return {
    id,
    state,
    progress,
    size: { width: size.first, height: size.second },
    iconToken: requiredString(fingerprint, "iconToken", `${id}.iconToken`),
    iconSizeToken: requiredString(fingerprint, "iconSizeToken", `${id}.iconSizeToken`),
    iconSizeMultiplier,
    scanLineThickness,
    colorToken: requiredString(stateConfig, "colorToken", `${id}.states.${state}.colorToken`),
  };
}

function recognitionState(value: string): FingerprintState {
  if (value === "initial" || value === "active" || value === "correct" || value === "incorrect") return value;
  throw new Error(`Unsupported Fingerprint state ${value}`);
}
