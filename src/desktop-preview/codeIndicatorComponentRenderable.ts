import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { boundedCenterBox, numberToken, renderScale } from "./componentRenderableCommon.js";
import type { CodeIndicatorDesignContract } from "./codeIndicatorComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function measureCodeIndicatorComponent(
  payload: DesignPreviewPayload,
  indicator: CodeIndicatorDesignContract,
) {
  const scale = renderScale(payload);
  const gap = Math.max(0, numberToken(payload, indicator.gapToken) * scale);
  return {
    width: indicator.glyphSize.width * scale * indicator.count + gap * Math.max(0, indicator.count - 1),
    height: indicator.glyphSize.height * scale,
    gap,
  };
}

export function codeIndicatorComponentToRenderable(
  payload: DesignPreviewPayload,
  indicator: CodeIndicatorDesignContract,
): RenderableNode {
  const size = measureCodeIndicatorComponent(payload, indicator);
  return codeIndicatorComponentToRenderableAt(
    payload,
    indicator,
    boundedCenterBox(payload, size.width, size.height),
  );
}

export function codeIndicatorComponentToRenderableAt(
  payload: DesignPreviewPayload,
  indicator: CodeIndicatorDesignContract,
  box: RenderableBox,
): RenderableNode {
  const size = measureCodeIndicatorComponent(payload, indicator);
  const glyphWidth = indicator.glyphSize.width * renderScale(payload);
  const glyphHeight = indicator.glyphSize.height * renderScale(payload);
  return {
    id: indicator.id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children: Array.from({ length: indicator.count }, (_, index) =>
      surfaceComponentToRenderableAt(
        payload,
        index < indicator.filledCount ? indicator.filledSurface : indicator.emptySurface,
        {
          x: box.x + index * (glyphWidth + size.gap),
          y: box.y + (box.height - glyphHeight) * 0.5,
          width: glyphWidth,
          height: glyphHeight,
        },
      )),
  };
}
