import { asRecord, parseObject, requiredNumber, requiredNumberPair, requiredString } from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { FaceRecognitionDesignContract, FaceRecognitionState } from "./faceRecognitionComponentContract.js";

export function resolveFaceRecognitionComponent(payload: DesignPreviewPayload): FaceRecognitionDesignContract {
  return resolveFaceRecognitionComponentFromRecords(parseObject(payload.configJson), parseObject(payload.designPreviewJson), "component.faceRecognition");
}

export function resolveFaceRecognitionComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  id: string,
): FaceRecognitionDesignContract {
  const face = asRecord(config.faceRecognition);
  const size = requiredNumberPair(face, "size", `${id}.size`);
  const state = recognitionState(requiredString(inputs, "state", `${id}.runtime.state`));
  const progress = requiredNumber(inputs, "progress", `${id}.runtime.progress`);
  if (size.first <= 0 || size.second <= 0) throw new Error(`${id}.size must be positive`);
  if (progress < 0 || progress > 1) throw new Error(`${id}.runtime.progress must be between 0 and 1`);
  const stateConfig = asRecord(asRecord(face.states)[state]);
  const strokeWidth = requiredNumber(face, "strokeWidth", `${id}.strokeWidth`);
  const iconSizeMultiplier = requiredNumber(face, "iconSizeMultiplier", `${id}.iconSizeMultiplier`);
  if (strokeWidth <= 0 || iconSizeMultiplier <= 0) throw new Error(`${id} strokeWidth and iconSizeMultiplier must be positive`);
  return {
    id,
    state,
    progress,
    size: { width: size.first, height: size.second },
    iconToken: requiredString(face, "iconToken", `${id}.iconToken`),
    iconSizeToken: requiredString(face, "iconSizeToken", `${id}.iconSizeToken`),
    iconSizeMultiplier,
    strokeWidth,
    colorToken: requiredString(stateConfig, "colorToken", `${id}.states.${state}.colorToken`),
  };
}

function recognitionState(value: string): FaceRecognitionState {
  if (value === "initial" || value === "active" || value === "correct" || value === "incorrect") return value;
  throw new Error(`Unsupported Face Recognition state ${value}`);
}
