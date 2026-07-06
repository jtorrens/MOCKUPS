import type { RenderableNode } from "../visual/renderable/types.js";
import { componentClassToRenderable } from "./componentClassRenderableRegistry.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function designPreviewPayloadToRenderable(
  payload: DesignPreviewPayload,
): RenderableNode {
  if (payload.kind === "componentClass") {
    return componentClassToRenderable(payload);
  }

  const box = {
    x: payload.previewFrame.screenX + payload.previewFrame.screenWidth * 0.16,
    y: payload.previewFrame.screenY + payload.previewFrame.screenHeight * 0.42,
    width: payload.previewFrame.screenWidth * 0.68,
    height: 88,
  };
  return {
    id: "design_preview.unsupported",
    type: "surface",
    frame: 0,
    box,
    text: `Unsupported design preview: ${payload.kind}`,
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
