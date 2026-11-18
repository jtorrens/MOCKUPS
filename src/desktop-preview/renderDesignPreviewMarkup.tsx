import { parseObject, requiredRecord, requiredNumber } from "./componentResolverCommon.js";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import { DesktopRenderableHtmlAdapter } from "./DesktopRenderableHtmlAdapter.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { designPreviewPayloadToRenderable } from "./designPreviewRenderableRegistry.js";
import { fontFacesForPayload } from "./previewAssetResolver.js";
import { withAuthoringTarget } from "./previewAuthoringTarget.js";
import { selectedColor } from "./previewColorHelpers.js";
import { rootPreviewScreenBox } from "./previewGeometryHelpers.js";
import { extractRootOverlays } from "./renderableRootOverlays.js";
import { screenTransitionLayers } from "./screenTransitionRenderable.js";

export function renderDesignPreviewRenderable(payload: DesignPreviewPayload): RenderableNode {
  const children = payload.kind === "screenTransition"
    ? screenTransitionChildren(payload)
    : screenContents(payload);

  return RenderableNodeSchema.parse(withAuthoringTarget(payload, {
    id: "design_preview.surface",
    type: "surface",
    frame: 0,
    box: {
      x: 0,
      y: 0,
      width: payload.previewFrame.canvasWidth,
      height: payload.previewFrame.canvasHeight,
    },
    style: {
      backgroundColor: previewCanvasBackground(payload),
    },
    metadata: {
      fontFaces: fontFacesForPayload(payload),
    },
    children,
  }));
}

export function renderDesignPreviewMarkup(payload: DesignPreviewPayload): string {
  const renderable = renderDesignPreviewRenderable(payload);
  return renderToStaticMarkup(
    React.createElement(DesktopRenderableHtmlAdapter, {
      tree: renderable,
      showBounds: payload.showMarks === true,
    }),
  );
}

function ownerChildren(payload: DesignPreviewPayload): RenderableNode[] {
  const child = designPreviewPayloadToRenderable(payload);
  const extracted = extractRootOverlays(child);
  return [extracted.node, ...extracted.overlays];
}

function verticalOffset(payload: DesignPreviewPayload): number {
  if (payload.kind !== "moduleInstance") return 0;
  const context = requiredRecord(parseObject(payload.instanceJson), "context", "Screen");
  return requiredNumber(context, "verticalOffsetPx", "Screen context");
}

function screenContents(payload: DesignPreviewPayload): RenderableNode[] {
  const children = ownerChildren(payload);
  const y = verticalOffset(payload);
  if (y === 0) return children;
  const box = rootPreviewScreenBox(payload);
  return [{
    id: "screen.viewport", type: "group", frame: payload.localFrame, box,
    style: { overflow: "hidden" },
    children: [{
      id: "screen.content", type: "group", frame: payload.localFrame, box,
      style: { backgroundColor: screenBackground(payload) },
      transform: { y }, children,
    }],
  }];
}

function screenTransitionChildren(payload: DesignPreviewPayload): RenderableNode[] {
  const transition = payload.screenTransition;
  if (!transition) {
    throw new Error("Screen transition payload is missing its resolved transition.");
  }
  const layers = transition.layers.map((layer, index) =>
    screenLayer(
      layer.owner,
      `design_preview.screen.layer.${index}`,
    ));
  return screenTransitionLayers(
    payload,
    transition,
    layers,
  );
}

function screenLayer(
  payload: DesignPreviewPayload,
  id: string,
): RenderableNode {
  const screenBox = rootPreviewScreenBox(payload);
  return {
    id,
    type: "group",
    frame: payload.localFrame,
    box: screenBox,
    style: {
      overflow: "hidden",
      backgroundColor: previewCanvasBackground(payload),
    },
    children: screenContents(payload),
  };
}

export function previewCanvasBackground(
  payload: DesignPreviewPayload,
): string | undefined {
  return verticalOffset(payload) === 0 ? screenBackground(payload) : undefined;
}

function screenBackground(payload: DesignPreviewPayload): string | undefined {
  return payload.kind === "screenTransition"
    || payload.previewFrame.moduleTransparency.enabled
    ? undefined
    : selectedColor(payload, "theme.colors.background");
}
