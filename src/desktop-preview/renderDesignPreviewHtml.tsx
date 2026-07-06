import { readFile } from "node:fs/promises";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { RenderableReactAdapter } from "../visual/adapters/react/RenderableReactAdapter.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { designPreviewPayloadToRenderable } from "./designPreviewRenderableRegistry.js";

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function resolvePaletteValue(payload: DesignPreviewPayload, value: unknown) {
  if (typeof value !== "string") return value;
  if (/^#|^rgb|^hsl|^transparent$/i.test(value)) return value;
  return payload.paletteColors?.[value] ?? value;
}

function resolvePaletteObject(
  payload: DesignPreviewPayload,
  value: Record<string, unknown>,
): Record<string, unknown> {
  return Object.fromEntries(
    Object.entries(value).map(([key, item]) => [
      key,
      typeof item === "object" && item !== null && !Array.isArray(item)
        ? resolvePaletteObject(payload, asRecord(item))
        : resolvePaletteValue(payload, item),
    ]),
  );
}

function parseObject(json: string, fallback: Record<string, unknown> = {}) {
  try {
    return asRecord(JSON.parse(json || "{}"));
  } catch {
    return fallback;
  }
}

function modeTokens(payload: DesignPreviewPayload) {
  const tokens = parseObject(payload.themeTokensJson);
  const modes = asRecord(tokens.modes);
  return resolvePaletteObject(payload, asRecord(modes[payload.themeMode]));
}

function themeBackground(payload: DesignPreviewPayload) {
  const colors = asRecord(modeTokens(payload).colors);
  const value = resolvePaletteValue(payload, colors.background);
  if (typeof value === "string" && value.trim()) return value;

  throw new Error(`Missing theme background color for mode ${payload.themeMode}`);
}

function renderableForPayload(payload: DesignPreviewPayload): RenderableNode {
  const child = designPreviewPayloadToRenderable(payload);

  return RenderableNodeSchema.parse({
    id: "design_preview_surface",
    type: "design_preview_surface",
    frame: 0,
    box: {
      x: 0,
      y: 0,
      width: payload.device.canvasWidth,
      height: payload.device.canvasHeight,
    },
    style: {
      backgroundColor: themeBackground(payload),
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
    React.createElement(RenderableReactAdapter, {
      tree: renderable,
      showBounds: payload.showMarks === true,
    }),
  );
  process.stdout.write(markup);
}

await main();
