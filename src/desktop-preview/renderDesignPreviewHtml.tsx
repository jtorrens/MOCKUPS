import { readFile } from "node:fs/promises";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import { DesktopRenderableHtmlAdapter } from "./DesktopRenderableHtmlAdapter.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { designPreviewPayloadToRenderable } from "./designPreviewRenderableRegistry.js";
import { selectedColor } from "./previewColorHelpers.js";

function renderableForPayload(payload: DesignPreviewPayload): RenderableNode {
  const child = designPreviewPayloadToRenderable(payload);

  return RenderableNodeSchema.parse({
    id: "design_preview_surface",
    type: "design_preview_surface",
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
    children: [child],
  });
}

async function main() {
  const inputPath = process.argv[2];
  if (!inputPath) {
    throw new Error("Missing design preview payload path.");
  }

  const payload = JSON.parse(
    await readFile(inputPath, "utf8"),
  ) as DesignPreviewPayload;
  const renderable = renderableForPayload(payload);
  const markup = renderToStaticMarkup(
    React.createElement(DesktopRenderableHtmlAdapter, {
      tree: renderable,
      showBounds: payload.showMarks === true,
    }),
  );
  process.stdout.write(markup);
}

await main();
