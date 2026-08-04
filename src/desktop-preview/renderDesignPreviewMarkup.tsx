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
    : ownerChildren(payload);

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

function screenTransitionChildren(payload: DesignPreviewPayload): RenderableNode[] {
  const transition = payload.screenTransition;
  if (!transition) {
    throw new Error("Screen transition payload is missing its resolved transition.");
  }
  const outgoing = screenLayer(
    transition.outgoing,
    "design_preview.screen.outgoing",
  );
  const incoming = screenLayer(
    transition.incoming,
    "design_preview.screen.incoming",
  );
  return screenTransitionLayers(
    payload,
    transition,
    outgoing,
    incoming,
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
    children: ownerChildren(payload),
  };
}

export function previewCanvasBackground(
  payload: DesignPreviewPayload,
): string | undefined {
  return payload.previewFrame.moduleTransparency.enabled
    ? undefined
    : selectedColor(payload, "theme.colors.background");
}
