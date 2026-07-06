import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  colorForMode,
  numberToken,
  renderScale,
  selectedColor,
  shadow,
  surfaceVisualPadding,
  variants,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export function surfaceComponentToRenderable(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const sampleWidth = surface.width * scale;
  const sampleHeight = surface.height * scale;
  const borderWidth = surface.surface.borderWidth * scale;
  const surfaceShadow = surface.surface.shadowEnabled ? shadow(payload) : undefined;
  const surfaceRelief = surfaceComponentRelief(surface, scale);
  const visualPadding = surfaceVisualPadding(
    borderWidth,
    surfaceShadow,
    surfaceRelief,
  );
  const groupBox = boundedCenterBox(
    payload,
    sampleWidth + visualPadding * 2,
    sampleHeight + visualPadding * 2,
  );
  const surfaceBox = {
    x: groupBox.x + visualPadding,
    y: groupBox.y + visualPadding,
    width: sampleWidth,
    height: sampleHeight,
  };

  return {
    id: surface.id,
    type: "group",
    role: "surfacePreview",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [surfaceComponentToRenderableAt(payload, surface, surfaceBox)],
  };
}

export function surfaceComponentToRenderableAt(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  box: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const surfaceShadow = surface.surface.shadowEnabled ? shadow(payload) : undefined;
  const surfaceRelief = surfaceComponentRelief(surface, scale);

  return {
    id: surface.id,
    type: "surface",
    role: "surface",
    frame: 0,
    box,
    style: {
      background: selectedColor(
        payload,
        surface.backgroundColorToken,
        surface.backgroundAlpha,
      ),
      borderColor: selectedColor(
        payload,
        surface.surface.borderColorToken,
        surface.borderAlpha,
      ),
      borderRadius: numberToken(payload, surface.surface.cornerRadiusToken) * scale,
      borderWidth: surface.surface.borderWidth * scale,
      shadow: surfaceShadow,
      surfaceRelief,
      colorModes: Object.fromEntries(
        variants(payload).map((mode) => [
          mode,
          {
            background: colorForMode(
              payload,
              surface.backgroundColorToken,
              mode,
              surface.backgroundAlpha,
            ),
            borderColor: colorForMode(
              payload,
              surface.surface.borderColorToken,
              mode,
              surface.borderAlpha,
            ),
          },
        ]),
      ),
    },
  };
}

function surfaceComponentRelief(
  surface: SurfaceDesignContract,
  scale: number,
) {
  return surface.surface.reliefEnabled
    ? {
        angleDeg: surface.surface.reliefAngle,
        extension: surface.surface.reliefExtent * scale,
        spread: surface.surface.reliefSpread * scale,
        upperIntensity:
          surface.surface.reliefTopIntensity * surface.backgroundAlpha,
        lowerIntensity:
          surface.surface.reliefBottomIntensity * surface.backgroundAlpha,
      }
    : undefined;
}
