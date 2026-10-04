import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { optionalBoolean, optionalNumber } from "./componentResolverCommon.js";
import { resolveParameterAnimation } from "./parameterAnimationResolver.js";
import type { ComponentMotionContract, ComponentMotionFrameContract } from "./previewComponentContracts.js";
import { motionTotalDurationMs, resolveMotionFrame } from "./previewMotionHelpers.js";
import type { RuntimeOwnerTimeline } from "./runtimeOwnerTimeline.js";

export interface RuntimeCollectionPresence {
  present: boolean;
  activationFrame?: number;
  exitFrame?: number;
  exitEndFrame?: number;
  reflowStartFrame?: number;
  reflowFromPresent: boolean;
  motionKind?: "enter" | "exit";
  motionFrame?: ComponentMotionFrameContract;
}

export function resolveRuntimeCollectionPresence(
  payload: DesignPreviewPayload,
  timeline: RuntimeOwnerTimeline,
  animation: Record<string, unknown>,
  item: Record<string, unknown>,
  targetId: string,
  screenFrame: number,
  enterMotion: ComponentMotionContract,
  exitMotion: ComponentMotionContract,
): RuntimeCollectionPresence {
  const hasTemporalOwner = timeline.ownsTarget(targetId);
  const ownerFrame = hasTemporalOwner
    ? Math.floor(timeline.temporalLocalFrame("present", targetId, screenFrame))
    : screenFrame;
  const resolved = resolveParameterAnimation(
    animation,
    "present",
    targetId,
    ownerFrame,
    item.present === true,
  );
  const present = resolved.value === true;
  const sourceFrame = resolved.sourceKeyframeFrame === undefined
    ? undefined
    : hasTemporalOwner
      ? timeline.screenFrame("present", targetId, resolved.sourceKeyframeFrame)
      : resolved.sourceKeyframeFrame;
  const enters = present
    && resolved.previousValue === false
    && sourceFrame !== undefined
    && sourceFrame > 0;
  const exits = !present
    && resolved.previousValue === true
    && sourceFrame !== undefined
    && sourceFrame > 0;
  const exitDurationFrames = Math.ceil(
    motionTotalDurationMs(payload, exitMotion) / 1000 * Math.max(1, payload.frameRate),
  );
  const exitFrame = exits
    && screenFrame - sourceFrame < exitDurationFrames
      ? sourceFrame
      : undefined;
  const exitEndFrame = exits
    ? sourceFrame + exitDurationFrames
    : undefined;
  const reflowStartFrame = exits
    ? exitEndFrame
    : enters ? sourceFrame : undefined;
  const explicitTransition = optionalBoolean(item, "presenceTransition");
  const explicitElapsedMs = Math.max(0, optionalNumber(item, "presenceElapsedMs", 0));
  const motion = present ? enterMotion : exitMotion;
  const motionState = explicitTransition
    ? {
        motionKind: present ? "enter" as const : "exit" as const,
        motionFrame: resolveMotionFrame(payload, motion, {
          trigger: true,
          elapsedMs: explicitElapsedMs,
        }),
      }
    : exitFrame !== undefined
      ? {
          motionKind: "exit" as const,
          motionFrame: resolveMotionFrame(payload, exitMotion, {
            trigger: true,
            elapsedMs: Math.max(0, screenFrame - exitFrame)
              / Math.max(1, payload.frameRate) * 1000,
          }),
        }
      : enters
        ? {
            motionKind: "enter" as const,
            motionFrame: resolveMotionFrame(payload, enterMotion, {
              trigger: true,
              elapsedMs: Math.max(0, screenFrame - sourceFrame)
                / Math.max(1, payload.frameRate) * 1000,
            }),
          }
        : {};
  return {
    present,
    activationFrame: present ? sourceFrame : undefined,
    exitFrame,
    exitEndFrame,
    reflowStartFrame,
    reflowFromPresent: exits,
    ...motionState,
  };
}
