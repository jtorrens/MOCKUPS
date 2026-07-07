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
import { surfaceShapeDataUri } from "./previewSurfaceShapeHelpers.js";
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
  if (surface.tail.enabled && surface.tail.width > 0 && surface.tail.height > 0) {
    return surfaceComponentTailRenderable(payload, surface, box);
  }

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

function surfaceComponentTailRenderable(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  box: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const cornerRadius = numberToken(payload, surface.surface.cornerRadiusToken) * scale;
  const background = selectedColor(
    payload,
    surface.backgroundColorToken,
    surface.backgroundAlpha,
  );
  const borderColor = selectedColor(
    payload,
    surface.surface.borderColorToken,
    surface.borderAlpha,
  );
  const surfaceShadow = surface.surface.shadowEnabled ? shadow(payload) : undefined;
  const shape = surfaceShapeDataUri({
    body: box,
    borderColor,
    borderWidth: surface.surface.borderWidth * scale,
    color: background,
    cornerRadius,
    tail: {
      cornerRadius,
      height: surface.tail.height * scale,
      side: surface.tail.side,
      style: surface.tail.style,
      vertical: surface.tail.vertical,
      width: surface.tail.width * scale,
    },
  });

  return {
    id: surface.id,
    type: "group",
    frame: 0,
    box,
    style: {
      overflow: "visible",
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
    children: [
      {
        id: `${surface.id}.shape`,
        type: "image",
        frame: 0,
        box: shape.box,
        asset: {
          type: "image",
          uri: shape.uri,
        },
        style: {
          filter: dropShadowFilter(surfaceShadow),
          objectFit: "fill",
          overflow: "visible",
        },
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

function dropShadowFilter(shadowValue: Record<string, unknown> | undefined) {
  if (!shadowValue) return undefined;
  const offsetX = typeof shadowValue.offsetX === "number" ? shadowValue.offsetX : 0;
  const offsetY = typeof shadowValue.offsetY === "number" ? shadowValue.offsetY : 0;
  const blur = typeof shadowValue.blur === "number" ? shadowValue.blur : 0;
  const color = typeof shadowValue.color === "string" ? shadowValue.color : "transparent";
  return `drop-shadow(${offsetX}px ${offsetY}px ${blur}px ${color})`;
}
