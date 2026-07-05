import { readFile } from "node:fs/promises";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { RenderableReactAdapter } from "../visual/adapters/react/RenderableReactAdapter.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveAvatarComponent } from "./avatarComponentResolver.js";
import { resolveButtonIconComponent } from "./buttonIconComponentResolver.js";
import { resolveLabelComponent } from "./labelComponentResolver.js";
import {
  avatarComponentToRenderable,
  buttonIconComponentToRenderable,
  labelComponentToRenderable,
  navigationBarToRenderable,
  statusBarToRenderable,
} from "./webPreviewBridge.js";
import {
  resolveNavigationBar,
  resolveStatusBar,
} from "./systemBarPreviewResolver.js";

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function readString(
  value: Record<string, unknown>,
  key: string,
  fallback = "",
) {
  const raw = value[key];
  return typeof raw === "string" ? raw : fallback;
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

function componentRenderableForPayload(
  payload: DesignPreviewPayload,
): RenderableNode {
  const preview = parseObject(payload.designPreviewJson ?? "{}");
  const componentType =
    payload.componentType || readString(preview, "componentType", "component");
  if (componentType === "label") {
    return labelComponentToRenderable(payload, resolveLabelComponent(payload));
  }
  if (componentType === "avatar") {
    return avatarComponentToRenderable(payload, resolveAvatarComponent(payload));
  }
  if (componentType === "buttonIcon") {
    return buttonIconComponentToRenderable(
      payload,
      resolveButtonIconComponent(payload),
    );
  }

  const box = {
    x: payload.device.screenX + payload.device.screenWidth * 0.16,
    y: payload.device.screenY + payload.device.screenHeight * 0.42,
    width: payload.device.screenWidth * 0.68,
    height: 88,
  };
  return {
    id: "component.preview.unsupported",
    type: "component_preview_unsupported",
    frame: 0,
    box,
    text: `Unsupported component preview: ${componentType}`,
    style: {
      backgroundColor: "#ff00ff",
      borderRadius: 6,
      color: "#ffffff",
      fontSize: 14,
      fontWeight: 700,
      lineHeight: box.height,
      textAlign: "center",
    },
    metadata: {
      route: "component-preview.unsupported",
      componentType,
    },
  };
}

function renderableForPayload(payload: DesignPreviewPayload): RenderableNode {
  if (payload.kind === "componentClass") {
    const component = componentRenderableForPayload(payload);
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
      children: [component],
    });
  }

  const child = payload.kind === "statusBar"
    ? statusBarToRenderable(payload, resolveStatusBar(payload))
    : navigationBarToRenderable(payload, resolveNavigationBar(payload));

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
