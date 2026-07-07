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
  const groupBox = boundedCenterBox(
    payload,
    sampleWidth,
    sampleHeight,
  );

  return surfaceComponentToRenderableAt(payload, surface, groupBox);
}

export function surfaceComponentToRenderableAt(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  box: RenderableBox,
): RenderableNode {
  const visualPadding = surfaceComponentVisualPadding(payload, surface);
  const surfaceNode = surfaceComponentSurfaceNode(payload, surface, box);
  if (visualPadding <= 0) {
    return surfaceNode;
  }

  return {
    id: surface.id,
    type: "group",
    frame: 0,
    box: {
      x: box.x - visualPadding,
      y: box.y - visualPadding,
      width: box.width + visualPadding * 2,
      height: box.height + visualPadding * 2,
    },
    style: {
      overflow: "visible",
    },
    children: [
      {
        ...surfaceNode,
        id: `${surface.id}.surface`,
      },
    ],
  };
}

function surfaceComponentSurfaceNode(
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

function surfaceComponentVisualPadding(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
) {
  const scale = renderScale(payload);
  const borderWidth = surface.surface.borderWidth * scale;
  const surfaceShadow = surface.surface.shadowEnabled ? shadow(payload) : undefined;
  const surfaceRelief = surfaceComponentRelief(surface, scale);
  return surfaceVisualPadding(borderWidth, surfaceShadow, surfaceRelief);
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
