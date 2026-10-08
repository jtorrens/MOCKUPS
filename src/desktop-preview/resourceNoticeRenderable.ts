import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";

/** Resource failure is resolved before painting, identically in Preview and export. */
export function resourceNoticeRenderable(id: string, box: RenderableBox, message: string): RenderableNode {
  return {
    id, type: "surface", frame: 0, box, text: message,
    style: {
      alignItems: "center", background: "rgba(0, 0, 0, 0.42)", color: "#ffffff",
      display: "flex", fontSize: Math.max(11, Math.min(box.width / 18, box.height * 0.055)),
      fontWeight: 700, justifyContent: "center", textAlign: "center", overflow: "hidden",
    },
  };
}
