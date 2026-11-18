import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { optionalBoolean, optionalNumber } from "./componentResolverCommon.js";
import type { ResolvedParameterAnimation } from "./parameterAnimationResolver.js";
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
  resolved: ResolvedParameterAnimation,
  item: Record<string, unknown>,
  targetId: string,
  screenFrame: number,
  enterMotion: ComponentMotionContract,
  exitMotion: ComponentMotionContract,
): RuntimeCollectionPresence {
  const hasTemporalOwner = timeline.ownsTarget(targetId);
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

/** Parent-owned bounded presence, shared by messages and floating components. */
export function resolveRuntimePresenceInterval(
  payload: DesignPreviewPayload,
  frame: number,
  startFrame: number,
  endFrame: number,
  hasExplicitEnd: boolean,
  automaticEndFrame: number,
  motion: ComponentMotionContract,
): { visible: boolean; presenceMotionKind?: "enter" | "exit"; presenceMotionFrame?: ComponentMotionFrameContract } {
  if (frame < startFrame || frame >= endFrame) return { visible: false };
  const duration = Math.ceil(motionTotalDurationMs(payload, motion) / 1000 * Math.max(1, payload.frameRate));
  const exitStart = Math.max(startFrame, endFrame - duration);
  const kind = hasExplicitEnd && endFrame < automaticEndFrame && frame >= exitStart
    ? "exit" : duration > 0 && frame - startFrame < duration ? "enter" : undefined;
  return { visible: true, ...(kind ? {
    presenceMotionKind: kind,
    presenceMotionFrame: resolveMotionFrame(payload, motion, {
      trigger: true,
      elapsedMs: Math.max(0, frame - (kind === "exit" ? exitStart : startFrame)) / Math.max(1, payload.frameRate) * 1000,
    }),
  } : {}) };
}
