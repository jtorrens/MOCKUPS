import type { RenderableNode } from "../visual/renderable/types.js";
import type {
  DesignPreviewPayload,
  ScreenTransitionPayload,
} from "./designPreviewPayload.js";
import { rootPreviewScreenBox } from "./previewGeometryHelpers.js";
import { parseObject } from "./previewJsonHelpers.js";
import {
  requiredMotionContract,
  resolveMotionFrame,
  wrapExitMotionFrame,
  wrapMotionFrame,
} from "./previewMotionHelpers.js";

export function screenTransitionLayers(
  payload: DesignPreviewPayload,
  transition: ScreenTransitionPayload,
  outgoing: RenderableNode,
  incoming: RenderableNode,
): RenderableNode[] {
  if (!Number.isFinite(transition.elapsedMilliseconds)
      || transition.elapsedMilliseconds < 0) {
    throw new Error("Screen transition elapsedMilliseconds must be non-negative.");
  }

  const outgoingMotion = requiredMotionContract(
    { motion: parseObject(transition.outgoingMotionJson, "outgoing Screen Motion") },
    "motion",
    "outgoing Screen Motion",
  );
  const incomingMotion = requiredMotionContract(
    { motion: parseObject(transition.incomingMotionJson, "incoming Screen Motion") },
    "motion",
    "incoming Screen Motion",
  );
  const screenBox = rootPreviewScreenBox(payload);
  const clock = {
    trigger: true,
    elapsedMs: transition.elapsedMilliseconds,
  };
  return [
    wrapExitMotionFrame(
      transition.outgoing,
      outgoing,
      outgoingMotion,
      resolveMotionFrame(
        transition.outgoing,
        outgoingMotion,
        clock,
      ),
      screenBox,
      screenBox,
    ),
    wrapMotionFrame(
      transition.incoming,
      incoming,
      incomingMotion,
      resolveMotionFrame(
        transition.incoming,
        incomingMotion,
        clock,
      ),
      screenBox,
      screenBox,
    ),
  ];
}
