import { optionalString, requiredString } from "./componentResolverCommon.js";
import { optionalObjectArray } from "./previewJsonHelpers.js";
import { requiredNumberValue } from "./previewValueHelpers.js";
import { textGraphemes } from "./previewTextRevealHelpers.js";
import { validateTransientAnimationDocument } from "./transientAnimationDocument.js";

type JsonRecord = Record<string, unknown>;

export type ResolvedParameterAnimation = {
  value: unknown;
  animated: boolean;
  sourceKeyframeFrame?: number;
  previousValue?: unknown;
};

export function resolveParameterAnimation(
  animation: JsonRecord,
  fieldId: string,
  targetId: string,
  frame: number,
  baseValue: unknown,
): ResolvedParameterAnimation {
  validateTransientAnimationDocument(animation);
  const track = optionalObjectArray(animation, "tracks", "runtime owner animation")
    .find((candidate) =>
      optionalString(candidate, "fieldId") === fieldId
      && optionalString(candidate, "targetId") === targetId);
  if (!track) return { value: baseValue, animated: false };

  const keyframes = optionalObjectArray(track, "keyframes", "runtime animation track")
    .filter((keyframe) => keyframe.enabled !== false)
    .map((keyframe) => ({
      frame: requiredNumberValue(keyframe.frame, "runtime animation keyframe frame"),
      value: keyframe.value,
      interpolation: Object.hasOwn(keyframe, "interpolation")
        ? requiredString(keyframe, "interpolation", "runtime animation keyframe interpolation")
        : "hold",
    }));
  if (keyframes.length === 0 || frame < keyframes[0]!.frame) {
    return { value: baseValue, animated: true };
  }

  const exact = keyframes.find((keyframe) => keyframe.frame === frame);
  if (exact) {
    const exactIndex = keyframes.indexOf(exact);
    return {
      value: exact.value,
      animated: true,
      sourceKeyframeFrame: exact.frame,
      previousValue: exactIndex > 0 ? keyframes[exactIndex - 1]!.value : baseValue,
    };
  }
  const destinationIndex = keyframes.findIndex((keyframe) => keyframe.frame > frame);
  if (destinationIndex < 0) {
    const final = keyframes[keyframes.length - 1]!;
    return {
      value: final.value,
      animated: true,
      sourceKeyframeFrame: final.frame,
      previousValue: keyframes.length > 1 ? keyframes[keyframes.length - 2]!.value : baseValue,
    };
  }

  const source = keyframes[destinationIndex - 1]!;
  const destination = keyframes[destinationIndex]!;
  const progress = (frame - source.frame) / Math.max(1, destination.frame - source.frame);
  if (destination.interpolation === "writeOn"
      && typeof source.value === "string"
      && typeof destination.value === "string") {
    return {
      value: rewriteText(source.value, destination.value, progress),
      animated: true,
      sourceKeyframeFrame: source.frame,
      previousValue: destinationIndex > 1 ? keyframes[destinationIndex - 2]!.value : baseValue,
    };
  }
  if ((destination.interpolation === "linear" || destination.interpolation === "easeInOut")
      && typeof source.value === "number"
      && typeof destination.value === "number") {
    const p = destination.interpolation === "easeInOut"
      ? progress * progress * (3 - 2 * progress)
      : progress;
    return {
      value: source.value + (destination.value - source.value) * p,
      animated: true,
      sourceKeyframeFrame: source.frame,
      previousValue: destinationIndex > 1 ? keyframes[destinationIndex - 2]!.value : baseValue,
    };
  }
  return {
    value: source.value,
    animated: true,
    sourceKeyframeFrame: source.frame,
    previousValue: destinationIndex > 1 ? keyframes[destinationIndex - 2]!.value : baseValue,
  };
}

function rewriteText(source: string, destination: string, progress: number) {
  const from = textGraphemes(source);
  const to = textGraphemes(destination);
  let common = 0;
  while (common < from.length && common < to.length && from[common] === to[common]) common += 1;
  const removals = from.length - common;
  const additions = to.length - common;
  const operationCount = removals + additions;
  const step = Math.max(0, Math.min(operationCount, Math.floor(operationCount * progress)));
  const removed = Math.min(removals, step);
  const appended = Math.max(0, step - removals);
  return from.slice(0, from.length - removed).concat(to.slice(common, common + appended)).join("");
}
