import type { RenderableNode } from "../visual/renderable/types.js";
import type {
  DesignPreviewPayload,
  ScreenTransitionPayload,
} from "./designPreviewPayload.js";
import type { ComponentMotionContract } from "./previewComponentContracts.js";
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
  layers: RenderableNode[],
): RenderableNode[] {
  if (layers.length !== transition.layers.length) {
    throw new Error("Screen transition layer payload and renderable counts differ.");
  }
  const screenBox = rootPreviewScreenBox(payload);
  return transition.layers.map((layer, index) => {
    if (!Number.isFinite(layer.elapsedMilliseconds)
        || layer.elapsedMilliseconds < 0) {
      throw new Error("Screen transition elapsedMilliseconds must be non-negative.");
    }
    if (layer.phase === "content") return layers[index]!;
    const motion = requiredMotionContract(
      { motion: parseObject(layer.motionJson, "Shot Screen Motion") },
      "motion",
      "Shot Screen Motion",
    );
    const layerMotion = layer.phase === "enter"
      ? {
          ...motion,
          direction: oppositeDirection(motion.direction),
        }
      : motion;
    const frame = resolveMotionFrame(
      layer.owner,
      layerMotion,
      {
        trigger: true,
        elapsedMs: layer.elapsedMilliseconds,
        durationMs: transition.durationFrames * 1000 / layer.owner.frameRate,
      },
    );
    return layer.phase === "exit"
      ? wrapExitMotionFrame(
          layer.owner,
          layers[index]!,
          layerMotion,
          frame,
          screenBox,
          screenBox,
        )
      : wrapMotionFrame(
          layer.owner,
          layers[index]!,
          layerMotion,
          frame,
          screenBox,
          screenBox,
        );
  });
}

function oppositeDirection(
  direction: ComponentMotionContract["direction"],
): ComponentMotionContract["direction"] {
  switch (direction) {
    case "top": return "bottom";
    case "bottom": return "top";
    case "left": return "right";
    case "right": return "left";
  }
}
