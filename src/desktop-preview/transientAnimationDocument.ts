import {
  optionalObjectArray,
  type JsonRecord,
} from "./previewJsonHelpers.js";
import { requiredNumberValue, requiredString } from "./previewValueHelpers.js";

const validatedDocuments = new WeakSet<JsonRecord>();
const interpolations = new Set(["hold", "linear", "easeInOut", "writeOn"]);

export function validateTransientAnimationDocument(animation: JsonRecord) {
  if (validatedDocuments.has(animation)) return;
  for (const key of Object.keys(animation)) {
    if (key !== "schemaVersion" && key !== "tracks") {
      throw new Error(`runtime owner animation contains undeclared property '${key}'`);
    }
  }
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
    let previousFrame = Number.NEGATIVE_INFINITY;
    for (const keyframe of optionalObjectArray(track, "keyframes", "runtime animation track")) {
      const frame = requiredNumberValue(keyframe.frame, "runtime animation keyframe frame");
      if (!Number.isInteger(frame)) {
        throw new Error("runtime animation keyframe frame must be an integer");
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

  validatedDocuments.add(animation);
}
