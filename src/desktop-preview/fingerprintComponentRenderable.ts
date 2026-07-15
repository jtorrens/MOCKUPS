import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { boundedCenterBox, iconTokenStyle, numberToken, renderScale, selectedColor } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { FingerprintDesignContract } from "./fingerprintComponentContract.js";

export function fingerprintComponentToRenderable(payload: DesignPreviewPayload, fingerprint: FingerprintDesignContract) {
  const size = measureFingerprintComponent(payload, fingerprint);
  return fingerprintComponentToRenderableAt(payload, fingerprint, boundedCenterBox(payload, size.width, size.height));
}

export function measureFingerprintComponent(payload: DesignPreviewPayload, fingerprint: FingerprintDesignContract) {
  const scale = renderScale(payload);
  return { width: fingerprint.size.width * scale, height: fingerprint.size.height * scale };
}

export function fingerprintComponentToRenderableAt(
  payload: DesignPreviewPayload,
  fingerprint: FingerprintDesignContract,
  box: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const iconSize = Math.min(box.width, box.height, Math.max(1, numberToken(payload, fingerprint.iconSizeToken) * fingerprint.iconSizeMultiplier * scale));
  const color = selectedColor(payload, fingerprint.colorToken);
  const lineHeight = Math.max(1, fingerprint.scanLineThickness * scale);
  const lineY = box.y + Math.max(0, box.height - lineHeight) * fingerprint.progress;
  return {
    id: fingerprint.id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children: [
      {
        id: `${fingerprint.id}.icon`,
        type: "icon",
        frame: 0,
        box: { x: box.x + (box.width - iconSize) / 2, y: box.y + (box.height - iconSize) / 2, width: iconSize, height: iconSize },
        text: fingerprint.iconToken,
        style: iconTokenStyle(payload, fingerprint.iconToken, color),
      },
      ...(fingerprint.state === "active" ? [{
        id: `${fingerprint.id}.scan`,
        type: "path" as const,
        frame: 0,
        box: { x: box.x, y: lineY, width: box.width, height: lineHeight },
        style: { fill: color, pathData: "M0 0H100V100H0Z", preserveAspectRatio: "none", viewBox: "0 0 100 100" },
      }] : []),
    ],
  };
}
