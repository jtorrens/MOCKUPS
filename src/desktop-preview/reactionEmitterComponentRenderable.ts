import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { fontFamilyForTypography } from "./previewFontHelpers.js";
import { placeChild, previewScreenBox, renderScale, rootPreviewScreenBox, scalePlacement } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ReactionEmitterDesignContract } from "./reactionEmitterComponentContract.js";

export function reactionEmitterComponentToRenderable(
  payload: DesignPreviewPayload, emitter: ReactionEmitterDesignContract, assignedBox?: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const fontFamily = fontFamilyForTypography(payload, "theme.emoji");
  const size = emitter.size * scale;
  const height = rootPreviewScreenBox(payload).height * emitter.riseHeightPercent / 100;
  const halfWidth = emitter.dispersionWidth * scale / 2;
  const box = assignedBox ?? previewScreenBox(payload);
  const origin = placeChild(box, { width: size, height: size }, scalePlacement(emitter.placement, scale));
  const children = emitter.particles.map((particle): RenderableNode => {
    const width = size * particle.scale;
    const x = origin.x + size / 2 + halfWidth * particle.drift;
    const y = origin.y + size / 2 - height * particle.rise;
    return { id: `emission.${particle.id}`, type: "text", frame: 0, text: particle.glyph,
      box: { x: x - width / 2, y: y - width / 2, width, height: width },
      style: { display: "flex", alignItems: "center", justifyContent: "center",
        fontFamily, fontSize: width, textAlign: "center", opacity: particle.opacity, whiteSpace: "nowrap" } };
  });
  return { id: "component.reactionEmitter", type: "group", frame: 0, box, style: {}, children };
}
