import { asRecord, optionalNumber, optionalString } from "./componentResolverCommon.js";
import { textGraphemes } from "./previewTextRevealHelpers.js";

type JsonRecord = Record<string, unknown>;

export type ResolvedParameterAnimation = {
  value: unknown;
  animated: boolean;
  sourceKeyframeFrame?: number;
};

export function resolveParameterAnimation(
  animation: JsonRecord,
  fieldId: string,
  targetId: string,
  frame: number,
  baseValue: unknown,
): ResolvedParameterAnimation {
  const track = (Array.isArray(animation.tracks) ? animation.tracks : [])
    .map(asRecord)
    .find((candidate) =>
      optionalString(candidate, "fieldId") === fieldId
      && optionalString(candidate, "targetId") === targetId);
  if (!track) return { value: baseValue, animated: false };

  const keyframes = (Array.isArray(track.keyframes) ? track.keyframes : [])
    .map(asRecord)
    .filter((keyframe) => keyframe.enabled !== false)
    .map((keyframe) => ({
      frame: Math.max(0, Math.floor(optionalNumber(keyframe, "frame", 0))),
      value: keyframe.value,
      interpolation: optionalString(keyframe, "interpolation") || "hold",
    }))
    .sort((a, b) => a.frame - b.frame);
  if (keyframes.length === 0 || frame < keyframes[0]!.frame) {
    return { value: baseValue, animated: true };
  }

  const exact = keyframes.find((keyframe) => keyframe.frame === frame);
  if (exact) {
    return { value: exact.value, animated: true, sourceKeyframeFrame: exact.frame };
  }
  const destinationIndex = keyframes.findIndex((keyframe) => keyframe.frame > frame);
  if (destinationIndex < 0) {
    const final = keyframes[keyframes.length - 1]!;
    return { value: final.value, animated: true, sourceKeyframeFrame: final.frame };
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
    };
  }
  return { value: source.value, animated: true, sourceKeyframeFrame: source.frame };
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
