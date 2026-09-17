import type { RenderableNode } from "../visual/renderable/types.js";
import {
  numberToken,
  previewScreenBox,
  renderScale,
} from "./componentRenderableCommon.js";
import type { ComponentStackChildRenderer } from "./componentStackComponentContract.js";
import { componentStackLayoutToRenderable } from "./componentStackComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { SurfaceStackDesignContract } from "./surfaceStackComponentContract.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function surfaceStackComponentToRenderable(
  payload: DesignPreviewPayload,
  stack: SurfaceStackDesignContract,
  renderChild: ComponentStackChildRenderer,
): RenderableNode {
  const scale = renderScale(payload);
  const screen = previewScreenBox(payload);
  const outerBox = {
    x: screen.x + (screen.width - stack.width * scale) * 0.5,
    y: screen.y + (screen.height - stack.height * scale) * 0.5,
    width: stack.width * scale,
    height: stack.height * scale,
  };
  const horizontalPadding = Math.max(0, numberToken(payload, stack.horizontalPaddingToken) * scale);
  const verticalPadding = Math.max(0, numberToken(payload, stack.verticalPaddingToken) * scale);
  const innerBox = {
    x: outerBox.x + horizontalPadding,
    y: outerBox.y + verticalPadding,
    width: Math.max(0, outerBox.width - horizontalPadding * 2),
    height: Math.max(0, outerBox.height - verticalPadding * 2),
  };
  const surface = surfaceComponentToRenderableAt(payload, stack.surface, outerBox);
  const content = componentStackLayoutToRenderable(payload, stack.layout, renderChild, innerBox);
  const radius = Math.max(0, numberToken(payload, stack.surface.surface.cornerRadiusToken) * scale);
  return {
    id: stack.id,
    type: "group",
    frame: 0,
    box: outerBox,
    style: { overflow: "visible" },
    children: [
      surface,
      {
        id: `${stack.id}.contentClip`,
        type: "group",
        frame: 0,
        box: outerBox,
        style: { overflow: "hidden", borderRadius: radius },
        children: [content],
      },
    ],
  };
}
