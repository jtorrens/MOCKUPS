import type { RenderableNode } from "../visual/renderable/types.js";
import { conversationModuleToRenderable } from "./conversationModuleRenderable.js";
import { lockScreenModuleToRenderable } from "./lockScreenModuleRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { applyRuntimeInputForwarding } from "./runtimeInputForwarding.js";

export function moduleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  payload = applyRuntimeInputForwarding(payload);
  if (payload.componentType === "module.core.chat") {
    return conversationModuleToRenderable(payload);
  }
  if (payload.componentType === "module.core.lockScreen") {
    return lockScreenModuleToRenderable(payload);
  }

  const box = {
    x: payload.previewFrame.screenX + payload.previewFrame.screenWidth * 0.16,
    y: payload.previewFrame.screenY + payload.previewFrame.screenHeight * 0.42,
    width: payload.previewFrame.screenWidth * 0.68,
    height: 88,
  };
  return {
    id: "module.preview.unsupported",
    type: "surface",
    frame: 0,
    box,
    text: `Unsupported module preview: ${payload.componentType}`,
    style: {
      alignItems: "center",
      backgroundColor: "#ff00ff",
      borderRadius: 6,
      color: "#ffffff",
      display: "flex",
      fontSize: 14,
      fontWeight: 700,
      justifyContent: "center",
      lineHeight: box.height,
      overflow: "hidden",
      paddingLeft: 12,
      paddingRight: 12,
      textAlign: "center",
      whiteSpace: "nowrap",
    },
  };
}
