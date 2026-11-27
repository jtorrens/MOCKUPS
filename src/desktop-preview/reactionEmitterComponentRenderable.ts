import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { fontFamilyForTypography } from "./previewFontHelpers.js";
import { boundedCenterBox, renderScale } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ReactionEmitterDesignContract } from "./reactionEmitterComponentContract.js";

export function reactionEmitterComponentToRenderable(
  payload: DesignPreviewPayload, emitter: ReactionEmitterDesignContract, assignedBox?: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const fontFamily = fontFamilyForTypography(payload, "theme.emoji");
  const size = emitter.size * scale;
  const height = emitter.height * scale;
  const spread = emitter.spread * scale;
  const box = assignedBox ?? boundedCenterBox(payload, size + 2 * spread, height + size);
  const children = emitter.particles.map((particle): RenderableNode => {
    const width = size * particle.scale;
    const x = box.x + box.width / 2 + spread * particle.drift;
    const y = box.y + box.height - size / 2 - height * particle.rise;
    return { id: `emission.${particle.id}`, type: "text", frame: 0, text: particle.glyph,
      box: { x: x - width / 2, y: y - width / 2, width, height: width },
      style: { display: "flex", alignItems: "center", justifyContent: "center",
        fontFamily, fontSize: width, textAlign: "center", opacity: particle.opacity, whiteSpace: "nowrap" } };
  });
  return { id: "component.reactionEmitter", type: "group", frame: 0, box, style: {}, children };
}
