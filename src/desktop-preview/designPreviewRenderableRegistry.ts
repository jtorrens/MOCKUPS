import type { RenderableNode } from "../visual/renderable/types.js";
import { componentClassToRenderable } from "./componentClassRenderableRegistry.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  navigationBarToRenderable,
  statusBarToRenderable,
} from "./systemBarRenderables.js";
import {
  resolveNavigationBar,
  resolveStatusBar,
} from "./systemBarPreviewResolver.js";

export function designPreviewPayloadToRenderable(
  payload: DesignPreviewPayload,
): RenderableNode {
  if (payload.kind === "componentClass") {
    return componentClassToRenderable(payload);
  }
  if (payload.kind === "statusBar") {
    return statusBarToRenderable(payload, resolveStatusBar(payload));
  }
  if (payload.kind === "navigationBar") {
    return navigationBarToRenderable(payload, resolveNavigationBar(payload));
  }

  const box = {
    x: payload.device.screenX + payload.device.screenWidth * 0.16,
    y: payload.device.screenY + payload.device.screenHeight * 0.42,
    width: payload.device.screenWidth * 0.68,
    height: 88,
  };
  return {
    id: "design_preview.unsupported",
    type: "component_preview_unsupported",
    frame: 0,
    box,
    text: `Unsupported design preview: ${payload.kind}`,
    style: {
      backgroundColor: "#ff00ff",
      borderRadius: 6,
      color: "#ffffff",
      fontSize: 14,
      fontWeight: 700,
      lineHeight: box.height,
      textAlign: "center",
    },
    metadata: {
      route: "design-preview.unsupported",
      kind: payload.kind,
    },
  };
}
