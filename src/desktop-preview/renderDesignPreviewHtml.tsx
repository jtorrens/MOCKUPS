import { existsSync, readFileSync } from "node:fs";
import { readFile } from "node:fs/promises";
import path from "node:path";
import React from "react";
import { renderToStaticMarkup } from "react-dom/server";
import { NavigationBarModule } from "../visual/modules/atomic/NavigationBarModule.js";
import { StatusBarModule } from "../visual/modules/atomic/StatusBarModule.js";
import { RenderableReactAdapter } from "../visual/adapters/react/RenderableReactAdapter.js";
import { RenderableNodeSchema } from "../visual/renderable/schema.js";
import type { RenderableNode } from "../visual/renderable/types.js";

interface DevicePayload {
  canvasWidth: number;
  canvasHeight: number;
  screenX: number;
  screenY: number;
  screenWidth: number;
  screenHeight: number;
  statusBarHeight?: number;
  safeAreaBottom?: number;
  scaleToPixels?: number;
}

interface DesignPreviewPayload {
  kind: "statusBar" | "navigationBar";
  configJson: string;
  device: DevicePayload;
  iconAssetRoot?: string;
  iconMappingJson?: string;
  paletteColors?: Record<string, string>;
  projectMediaRoot?: string;
  themeMode: "light" | "dark";
  themeTokensJson: string;
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function readNumber(
  value: Record<string, unknown>,
  key: string,
  fallback: number,
) {
  const raw = value[key];
  return typeof raw === "number" && Number.isFinite(raw) ? raw : fallback;
}

function renderScale(payload: DesignPreviewPayload) {
  const scale = payload.device.scaleToPixels;
  return typeof scale === "number" && Number.isFinite(scale) && scale > 0
    ? scale
    : 1;
}

function resolvePaletteValue(payload: DesignPreviewPayload, value: unknown) {
  if (typeof value !== "string") return value;
  if (/^#|^rgb|^hsl|^transparent$/i.test(value)) return value;
  return payload.paletteColors?.[value] ?? value;
}

function resolvePaletteObject(
  payload: DesignPreviewPayload,
  value: Record<string, unknown>,
) {
  return Object.fromEntries(
    Object.entries(value).map(([key, item]) => [
      key,
      typeof item === "object" && item !== null && !Array.isArray(item)
        ? resolvePaletteObject(payload, asRecord(item))
        : resolvePaletteValue(payload, item),
    ]),
  );
}

function scaleLayout(
  layout: Record<string, unknown>,
  scale: number,
  keys: string[],
) {
  return {
    ...layout,
    ...Object.fromEntries(
      keys
        .filter((key) => typeof layout[key] === "number")
        .map((key) => [key, (layout[key] as number) * scale]),
    ),
  };
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
  return typeof value === "string" && value.trim()
    ? value
    : payload.themeMode === "dark"
      ? "#101827"
      : "#F7F9FC";
}

function statusBarTokens(payload: DesignPreviewPayload) {
  const statusBar = asRecord(modeTokens(payload).statusBar);
  return {
    foreground:
      typeof statusBar.foreground === "string" ? statusBar.foreground : "#111827",
    background:
      typeof statusBar.background === "object" && statusBar.background !== null
        ? (asRecord(statusBar.background).color as string | undefined) ?? "transparent"
        : typeof statusBar.background === "string"
          ? statusBar.background
          : "transparent",
  };
}

function navigationBarTokens(payload: DesignPreviewPayload) {
  const navigationBar = asRecord(modeTokens(payload).navigationBar);
  return {
    foreground:
      typeof navigationBar.foreground === "string"
        ? navigationBar.foreground
        : "#111827",
    background:
      typeof navigationBar.background === "object" && navigationBar.background !== null
        ? (asRecord(navigationBar.background).color as string | undefined) ?? "transparent"
        : typeof navigationBar.background === "string"
          ? navigationBar.background
          : "transparent",
  };
}

function iconUriForToken(payload: DesignPreviewPayload, token: string) {
  const mapping = parseObject(payload.iconMappingJson ?? "{}");
  const tokens = asRecord(mapping.tokens);
  const iconToken = asRecord(tokens[token]);
  const file = typeof iconToken.file === "string" ? iconToken.file : "";
  const assetRoot = payload.iconAssetRoot?.replace(/\/+$/g, "") ?? "";
  if (!file || !assetRoot) return "";

  const candidates = [
    path.resolve(payload.projectMediaRoot ?? "", assetRoot, file),
    path.resolve("assets/FOQN_S2", assetRoot, file),
    path.resolve("assets", assetRoot, file),
    path.resolve(assetRoot, file),
  ];
  const fullPath = candidates.find((candidate) => existsSync(candidate));
  if (!fullPath) return "";

  const svg = readFileSync(fullPath);
  return `data:image/svg+xml;base64,${svg.toString("base64")}`;
}

function resolveStatusBarItems(
  payload: DesignPreviewPayload,
  config: Record<string, unknown>,
) {
  const items = Array.isArray(config.items) ? config.items : [];
  return items.map((item) => {
    const row = asRecord(item);
    const kind = typeof row.kind === "string" ? row.kind : "";
    const token = typeof row.token === "string" ? row.token : "";
    const iconUri = kind === "iconToken" && token ? iconUriForToken(payload, token) : "";
    return iconUri ? { ...row, iconUri } : row;
  });
}

function renderableForPayload(payload: DesignPreviewPayload): RenderableNode {
  const config = parseObject(payload.configJson);
  const viewport = {
    x: payload.device.screenX,
    y: payload.device.screenY,
    width: payload.device.screenWidth,
    height: payload.device.screenHeight,
  };
  const scale = renderScale(payload);
  const rawLayout = asRecord(config.layout);
  const configForRender =
    payload.kind === "statusBar"
      ? {
          ...config,
          layout: scaleLayout(rawLayout, scale, [
            "height",
            "itemSize",
            "gap",
            "sidePadding",
          ]),
          items: resolveStatusBarItems(payload, config),
        }
      : {
          ...config,
          layout: scaleLayout(rawLayout, scale, [
            "height",
            "itemSize",
            "sidePadding",
            "strokeWidth",
            "cornerRadius",
          ]),
          gesture: scaleLayout(asRecord(config.gesture), scale, [
            "width",
            "height",
            "cornerRadius",
          ]),
        };
  const layout = asRecord(configForRender.layout);
  const statusBarHeight =
    readNumber(
      layout,
      "height",
      typeof payload.device.statusBarHeight === "number" &&
        Number.isFinite(payload.device.statusBarHeight) &&
        payload.device.statusBarHeight > 0
        ? payload.device.statusBarHeight
        : 54,
    );
  const navigationBarHeight = readNumber(layout, "height", 0);
  const child =
    payload.kind === "statusBar"
      ? {
          ...StatusBarModule.render({
            frame: 0,
            viewport,
            statusBarHeight,
            statusBar: configForRender,
            tokens: statusBarTokens(payload),
          }),
          box: {
            x: viewport.x,
            y: viewport.y,
            width: viewport.width,
            height: statusBarHeight,
          },
        }
      : {
          ...NavigationBarModule.render({
            frame: 0,
            viewport,
            navigationBar: configForRender,
            tokens: navigationBarTokens(payload),
          }),
          box: {
            x: viewport.x,
            y: viewport.y + viewport.height - navigationBarHeight,
            width: viewport.width,
            height: navigationBarHeight,
          },
        };

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
    React.createElement(RenderableReactAdapter, { tree: renderable }),
  );
  process.stdout.write(markup);
}

await main();
