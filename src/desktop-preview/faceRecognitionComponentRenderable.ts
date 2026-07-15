import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { boundedCenterBox, iconTokenStyle, numberToken, renderScale, selectedColor } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { FaceRecognitionDesignContract } from "./faceRecognitionComponentContract.js";

export function faceRecognitionComponentToRenderable(payload: DesignPreviewPayload, face: FaceRecognitionDesignContract) {
  const size = measureFaceRecognitionComponent(payload, face);
  return faceRecognitionComponentToRenderableAt(payload, face, boundedCenterBox(payload, size.width, size.height));
}

export function measureFaceRecognitionComponent(payload: DesignPreviewPayload, face: FaceRecognitionDesignContract) {
  const scale = renderScale(payload);
  return { width: face.size.width * scale, height: face.size.height * scale };
}

export function faceRecognitionComponentToRenderableAt(
  payload: DesignPreviewPayload,
  face: FaceRecognitionDesignContract,
  box: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const iconSize = Math.min(box.width, box.height, Math.max(1, numberToken(payload, face.iconSizeToken) * face.iconSizeMultiplier * scale));
  const color = selectedColor(payload, face.colorToken);
  const scanY = 12 + 76 * face.progress;
  return {
    id: face.id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children: [
      {
        id: `${face.id}.icon`, type: "icon", frame: 0,
        box: { x: box.x + (box.width - iconSize) / 2, y: box.y + (box.height - iconSize) / 2, width: iconSize, height: iconSize },
        text: face.iconToken, style: iconTokenStyle(payload, face.iconToken, color),
      },
      {
        id: `${face.id}.frame`, type: "path", frame: 0, box,
        style: {
          fill: "none", stroke: color, strokeWidth: Math.max(1, face.strokeWidth * scale), strokeLinecap: "round", strokeLinejoin: "round",
          pathData: `M28 8H8V28 M72 8H92V28 M8 72V92H28 M92 72V92H72${face.state === "active" ? ` M16 ${scanY}H84` : ""}`,
          viewBox: "0 0 100 100", preserveAspectRatio: "none",
        },
      },
    ],
  };
}
