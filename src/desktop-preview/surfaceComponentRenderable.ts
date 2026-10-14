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
import { resolvePaletteColor, signedColorBlend } from "./previewColorHelpers.js";

export interface SurfaceColorOverride {
  background: string;
  borderColor?: string;
  colorModes?: Record<string, { background: string; borderColor?: string }>;
}

type SurfacePaintLayer = "complete" | "background" | "border";

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
  colors?: SurfaceColorOverride,
): RenderableNode {
  return surfaceComponentToRenderableAtWithColors(payload, surface, box, colors);
}

export function surfaceComponentToRenderableAtWithColors(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  box: RenderableBox,
  colors?: SurfaceColorOverride,
): RenderableNode {
  return surfaceComponentPaintLayerAt(payload, surface, box, "complete", colors);
}

export function surfaceComponentBackgroundToRenderableAt(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  box: RenderableBox,
): RenderableNode {
  return surfaceComponentPaintLayerAt(payload, surface, box, "background");
}

export function surfaceComponentBorderToRenderableAt(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  box: RenderableBox,
): RenderableNode {
  return surfaceComponentPaintLayerAt(payload, surface, box, "border");
}

function surfaceComponentPaintLayerAt(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  box: RenderableBox,
  layer: SurfacePaintLayer,
  colors?: SurfaceColorOverride,
): RenderableNode {
  const resolvedColors = surfaceLayerColors(surfacePaintColors(payload, surface, colors), layer);
  if (surface.tail.enabled && surface.tail.width > 0 && surface.tail.height > 0) {
    return surfaceComponentTailRenderable(payload, surface, box, resolvedColors, layer);
  }

  const visualPadding = surfaceComponentVisualPadding(payload, surface, layer);
  const surfaceNode = surfaceComponentSurfaceNode(payload, surface, box, resolvedColors, layer);
  if (visualPadding <= 0) {
    return surfaceNode;
  }

  return {
    id: surfaceLayerId(surface.id, layer),
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
        id: `${surfaceLayerId(surface.id, layer)}.surface`,
      },
    ],
  };
}

function surfaceComponentTailRenderable(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  box: RenderableBox,
  colors: Required<SurfaceColorOverride>,
  layer: SurfacePaintLayer,
): RenderableNode {
  const scale = renderScale(payload);
  const cornerRadius = numberToken(payload, surface.surface.cornerRadiusToken) * scale;
  const background = colors.background;
  const borderColor = colors.borderColor;
  const surfaceShadow = layer !== "border" && surface.surface.shadowEnabled ? shadow(payload) : undefined;
  const surfaceRelief = layer !== "border" ? surfaceComponentRelief(surface, scale) : undefined;
  const shape = surfaceShapeDataUri({
    body: box,
    borderColor,
    borderWidth: layer === "background" ? 0 : surface.surface.borderWidth * scale,
    color: background,
    cornerRadius,
    relief: surfaceRelief,
    tail: {
      cornerRadius,
      height: surface.tail.height * scale,
      outerCornerRadius: surface.tail.outerCornerRadius * scale,
      side: surface.tail.side,
      style: surface.tail.style,
      vertical: surface.tail.vertical,
      width: surface.tail.width * scale,
    },
  });

  return {
    id: surfaceLayerId(surface.id, layer),
    type: "group",
    frame: 0,
    box,
    style: {
      overflow: "visible",
      colorModes: colors.colorModes,
    },
    children: [
      {
        id: `${surfaceLayerId(surface.id, layer)}.shape`,
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
  colors: Required<SurfaceColorOverride>,
  layer: SurfacePaintLayer,
): RenderableNode {
  const scale = renderScale(payload);
  const surfaceShadow = layer !== "border" && surface.surface.shadowEnabled ? shadow(payload) : undefined;
  const surfaceRelief = layer !== "border" ? surfaceComponentRelief(surface, scale) : undefined;

  return {
    id: surfaceLayerId(surface.id, layer),
    type: "surface",
    frame: 0,
    box,
    style: {
      background: colors.background,
      borderColor: colors.borderColor,
      borderRadius: numberToken(payload, surface.surface.cornerRadiusToken) * scale,
      borderWidth: layer === "background" ? 0 : surface.surface.borderWidth * scale,
      shadow: surfaceShadow,
      surfaceRelief,
      colorModes: colors.colorModes,
    },
  };
}

function surfaceLayerId(id: string, layer: SurfacePaintLayer) {
  return layer === "complete" ? id : `${id}.${layer}`;
}

function surfaceLayerColors(
  colors: Required<SurfaceColorOverride>,
  layer: SurfacePaintLayer,
): Required<SurfaceColorOverride> {
  if (layer === "complete") return colors;
  const select = (values: { background: string; borderColor?: string }) => layer === "background"
    ? { background: values.background, borderColor: "transparent" }
    : { background: "transparent", borderColor: values.borderColor ?? "transparent" };
  return {
    ...select(colors),
    colorModes: Object.fromEntries(
      Object.entries(colors.colorModes).map(([mode, values]) => [mode, select(values)]),
    ),
  };
}

function surfacePaintColors(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  colors: SurfaceColorOverride | undefined,
): Required<SurfaceColorOverride> {
  const tint = resolvePaletteColor(payload, surface.tintPaletteColor);
  const blend = (background: string) => signedColorBlend(background, tint, surface.tintAmount);
  const modes = colors?.colorModes ?? surfaceColorModes(payload, surface);
  return {
    background: blend(colors?.background
      ?? selectedColor(payload, surface.backgroundColorToken, surface.backgroundAlpha)),
    borderColor: colors?.borderColor
      ?? selectedColor(payload, surface.surface.borderColorToken, surface.borderAlpha),
    colorModes: Object.fromEntries(Object.entries(modes).map(([mode, values]) => [
      mode, { ...values, background: blend(values.background) },
    ])),
  };
}

function surfaceColorModes(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
) {
  return Object.fromEntries(
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
  );
}

function surfaceComponentVisualPadding(
  payload: DesignPreviewPayload,
  surface: SurfaceDesignContract,
  layer: SurfacePaintLayer,
) {
  const scale = renderScale(payload);
  const borderWidth = layer === "background" ? 0 : surface.surface.borderWidth * scale;
  const surfaceShadow = layer !== "border" && surface.surface.shadowEnabled ? shadow(payload) : undefined;
  const surfaceRelief = layer !== "border" ? surfaceComponentRelief(surface, scale) : undefined;
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
