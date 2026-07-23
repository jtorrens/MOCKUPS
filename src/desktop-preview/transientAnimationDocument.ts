import {
  isRecord,
  optionalObject,
  optionalObjectArray,
  type JsonRecord,
} from "./previewJsonHelpers.js";
import { requiredNumberValue, requiredString } from "./previewValueHelpers.js";

const validatedDocuments = new WeakSet<JsonRecord>();
const interpolations = new Set(["hold", "linear", "easeInOut", "writeOn"]);

export function validateTransientAnimationDocument(animation: JsonRecord) {
  if (validatedDocuments.has(animation)) return;
  const trackTargets = new Set<string>();
  for (const track of optionalObjectArray(animation, "tracks", "runtime owner animation")) {
    const fieldId = requiredString(track, "fieldId", "runtime animation track field id");
    let targetId = "";
    if (Object.hasOwn(track, "targetId")) {
      if (typeof track.targetId !== "string" || (track.targetId.length > 0 && !track.targetId.trim())) {
        throw new Error("runtime animation track target id must be a stable string or the Screen sentinel");
      }
      targetId = track.targetId;
    }
    const trackTarget = JSON.stringify([fieldId, targetId]);
    if (trackTargets.has(trackTarget)) {
      throw new Error(`runtime animation contains duplicate track target '${fieldId}'/'${targetId}'`);
    }
    trackTargets.add(trackTarget);
    const frames = new Set<number>();
    let previousFrame = -1;
    for (const keyframe of optionalObjectArray(track, "keyframes", "runtime animation track")) {
      const frame = requiredNumberValue(keyframe.frame, "runtime animation keyframe frame");
      if (!Number.isInteger(frame) || frame < 0) {
        throw new Error("runtime animation keyframe frame must be a non-negative integer");
      }
      if (frames.has(frame)) {
        throw new Error(`runtime animation track '${fieldId}'/'${targetId}' contains duplicate frame ${frame}`);
      }
      frames.add(frame);
      if (frame < previousFrame) {
        throw new Error(`runtime animation track '${fieldId}'/'${targetId}' keyframes must be ordered by frame`);
      }
      previousFrame = frame;
      if (Object.hasOwn(keyframe, "enabled") && typeof keyframe.enabled !== "boolean") {
        throw new Error("runtime animation keyframe enabled must be a boolean when present");
      }
      if (!Object.hasOwn(keyframe, "value")) {
        throw new Error("runtime animation keyframe requires an explicit value");
      }
      if (Object.hasOwn(keyframe, "interpolation")) {
        const interpolation = requiredString(
          keyframe,
          "interpolation",
          "runtime animation keyframe interpolation",
        );
        if (!interpolations.has(interpolation)) {
          throw new Error(`Unsupported runtime animation keyframe interpolation '${interpolation}'`);
        }
      }
    }
  }

  const retime = optionalObject(animation, "retime", "runtime owner animation");
  validateOptionalPositiveFrameCount(retime, "targetDurationFrames", "runtime animation retime");
  const targets = optionalObject(retime, "targets", "runtime animation retime");
  for (const [targetId, value] of Object.entries(targets)) {
    if (!targetId.trim() || !isRecord(value)) {
      throw new Error("runtime animation retime target must be a named object");
    }
    validateOptionalPositiveFrameCount(
      value,
      "targetDurationFrames",
      `runtime animation retime target '${targetId}'`,
    );
  }
  validatedDocuments.add(animation);
}

function validateOptionalPositiveFrameCount(owner: JsonRecord, key: string, path: string) {
  if (!Object.hasOwn(owner, key)) return;
  const value = requiredNumberValue(owner[key], `${path} '${key}'`);
  if (!Number.isInteger(value) || value <= 0) {
    throw new Error(`${path} '${key}' must be a positive integer`);
  }
}
