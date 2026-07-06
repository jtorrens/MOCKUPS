import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  renderScale,
  selectedColor,
} from "./componentRenderableCommon.js";
import type { CursorDesignContract } from "./cursorComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function cursorComponentToRenderable(
  payload: DesignPreviewPayload,
  cursor: CursorDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const box = boundedCenterBox(
    payload,
    Math.max(1, cursor.width * scale),
    Math.max(1, cursor.height * scale),
  );

  return cursorComponentToRenderableAt(payload, cursor, box);
}

export function cursorComponentToRenderableAt(
  payload: DesignPreviewPayload,
  cursor: CursorDesignContract,
  box: RenderableBox,
): RenderableNode {
  return {
    id: cursor.id,
    type: "path",
    frame: 0,
    box,
    style: {
      fill: selectedColor(payload, cursor.colorToken),
      pathData: "M0 0H100V100H0Z",
      preserveAspectRatio: "none",
      viewBox: "0 0 100 100",
      opacity: 1,
    },
  };
}
