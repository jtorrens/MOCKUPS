import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import { DesktopRenderableHtmlAdapter } from "./DesktopRenderableHtmlAdapter.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { designPreviewPayloadToRenderable } from "./designPreviewRenderableRegistry.js";
import { fontFacesForPayload } from "./previewAssetResolver.js";
import { selectedColor } from "./previewColorHelpers.js";

function renderableForPayload(payload: DesignPreviewPayload): RenderableNode {
  const child = designPreviewPayloadToRenderable(payload);

  return RenderableNodeSchema.parse({
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
      backgroundColor: selectedColor(payload, "theme.colors.background"),
    },
    metadata: {
      fontFaces: fontFacesForPayload(payload),
    },
    children: [child],
  });
}

export function renderDesignPreviewMarkup(payload: DesignPreviewPayload): string {
  const renderable = renderableForPayload(payload);
  return renderToStaticMarkup(
    React.createElement(DesktopRenderableHtmlAdapter, {
      tree: renderable,
      showBounds: payload.showMarks === true,
    }),
  );
}
