import { existsSync, readFileSync } from "node:fs";
import path from "node:path";
import type { RenderableBox } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { AlignmentPlacementContract } from "./componentResolverCommon.js";

type JsonRecord = Record<string, unknown>;

function asRecord(value: unknown): JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as JsonRecord)
    : {};
}

function parseObject(json: string | undefined) {
  return asRecord(JSON.parse(json || "{}"));
}

export function renderScale(payload: DesignPreviewPayload) {
  const scale = payload.device.scaleToPixels;
  return typeof scale === "number" && Number.isFinite(scale) && scale > 0
    ? scale
    : 1;
}

export function variants(payload: DesignPreviewPayload) {
  const tokens = parseObject(payload.themeTokensJson);
  const modes = asRecord(tokens.modes);
  const names = Object.keys(modes);
  return names.length > 0 ? names : [payload.themeMode || "light"];
}

function tokenValueForMode(
  payload: DesignPreviewPayload,
  token: string,
  mode: string,
) {
  if (!token.startsWith("theme.")) {
    throw new Error(`Unsupported theme token ${token}`);
  }

  const root = parseObject(payload.themeTokensJson);
  const modeRoot = asRecord(asRecord(root.modes)[mode]);
  const semanticKey = token.replace(/^theme\./, "");
  const parts = semanticKey.split(".");
  for (const source of [modeRoot, root]) {
    let current: unknown = source;
    for (const part of parts) {
      current = asRecord(current)[part];
    }
    if (current !== undefined) return current;

    const colors = asRecord(asRecord(source).colors);
    if (colors[semanticKey] !== undefined) return colors[semanticKey];
    if (colors[token] !== undefined) return colors[token];
  }

  throw new Error(`Missing theme token ${token} for mode ${mode}`);
}

function resolvePaletteColor(payload: DesignPreviewPayload, value: unknown) {
  if (typeof value !== "string" || !value.trim()) {
    throw new Error("Missing palette color value");
  }
  if (/^#|^rgb|^hsl|^transparent$/i.test(value)) return value;
  const resolved = payload.paletteColors?.[value];
  if (!resolved) throw new Error(`Missing palette color ${value}`);
  return payload.paletteNeutralColors?.[value]
    ? applyNeutralTint(payload, resolved)
    : resolved;
}

export function colorForMode(
  payload: DesignPreviewPayload,
  token: string,
  mode: string,
  alpha = 1,
) {
  const raw = tokenValueForMode(payload, token, mode);
  const color =
    typeof raw === "object" && raw !== null && !Array.isArray(raw)
      ? resolvePaletteColor(payload, asRecord(raw).color)
      : resolvePaletteColor(payload, raw);
  const rawAlpha =
    typeof raw === "object" && raw !== null && !Array.isArray(raw)
      ? numberValue(asRecord(raw).alpha, 1)
      : 1;
  return cssColorWithAlpha(color, rawAlpha * alpha);
}

export function selectedColor(payload: DesignPreviewPayload, token: string, alpha = 1) {
  return colorForMode(payload, token, payload.themeMode || "light", alpha);
}

export function selectedPaletteColor(payload: DesignPreviewPayload, token: string, alpha = 1) {
  return cssColorWithAlpha(resolvePaletteColor(payload, token), alpha);
}

export function iconTokenStyle(
  payload: DesignPreviewPayload,
  token: string,
  color: string,
) {
  const iconUri = iconUriForToken(payload, token);
  return {
    color,
    ...(iconUri
      ? {
          maskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
          WebkitMaskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
        }
      : {}),
  };
}

export function numberToken(payload: DesignPreviewPayload, token: string) {
  const raw = tokenValueForMode(payload, token, payload.themeMode || "light");
  const value = numberValue(raw, NaN);
  if (Number.isFinite(value)) return value;
  throw new Error(`Theme token ${token} is not numeric`);
}

export function numberValue(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export function stringValue(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function requiredNumberValue(value: unknown, path: string) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  throw new Error(`Missing numeric theme value ${path}`);
}

export function shadow(payload: DesignPreviewPayload) {
  const root = parseObject(payload.themeTokensJson);
  const shadowRoot = asRecord(asRecord(root.shadows).default);
  const color = asRecord(shadowRoot.color);
  const colorToken = color.color;
  if (typeof colorToken !== "string" || !colorToken.trim()) {
    throw new Error("Missing theme.shadows.default.color.color");
  }

  const scale = renderScale(payload);
  return {
    offsetX:
      requiredNumberValue(shadowRoot.offsetX, "theme.shadows.default.offsetX") *
      scale,
    offsetY:
      requiredNumberValue(shadowRoot.offsetY, "theme.shadows.default.offsetY") *
      scale,
    blur:
      requiredNumberValue(shadowRoot.blur, "theme.shadows.default.blur") *
      scale,
    color: cssColorWithAlpha(
      resolvePaletteColor(payload, colorToken),
      requiredNumberValue(color.alpha, "theme.shadows.default.color.alpha"),
    ),
  };
}

export function centerBox(payload: DesignPreviewPayload, width: number, height: number) {
  const { device } = payload;
  return {
    x: device.screenX + (device.screenWidth - width) / 2,
    y: device.screenY + (device.screenHeight - height) / 2,
    width,
    height,
  };
}

export function surfaceVisualPadding(
  borderWidth: number,
  shadowValue: Record<string, unknown> | undefined,
  surfaceRelief: Record<string, unknown> | undefined,
) {
  const shadowPadding = shadowValue
    ? Math.max(
        Math.abs(typeof shadowValue.offsetX === "number" ? shadowValue.offsetX : 0),
        Math.abs(typeof shadowValue.offsetY === "number" ? shadowValue.offsetY : 0),
      ) + (typeof shadowValue.blur === "number" ? shadowValue.blur * 2 : 0)
    : 0;
  const reliefPadding = surfaceRelief
    ? Math.max(
        typeof surfaceRelief.extension === "number" ? surfaceRelief.extension : 0,
        typeof surfaceRelief.spread === "number" ? surfaceRelief.spread : 0,
      )
    : 0;
  return Math.ceil(Math.max(borderWidth, shadowPadding, reliefPadding, 0));
}

export function boundedCenterBox(
  payload: DesignPreviewPayload,
  width: number,
  height: number,
) {
  const centered = centerBox(payload, width, height);
  const minX = payload.device.screenX;
  const minY = payload.device.screenY;
  const maxX = payload.device.screenX + payload.device.screenWidth - width;
  const maxY = payload.device.screenY + payload.device.screenHeight - height;
  return {
    x: maxX >= minX ? Math.min(Math.max(centered.x, minX), maxX) : minX,
    y: maxY >= minY ? Math.min(Math.max(centered.y, minY), maxY) : minY,
    width,
    height,
  };
}

export function scalePlacement(
  placement: AlignmentPlacementContract,
  scale: number,
): AlignmentPlacementContract {
  return {
    ...placement,
    offsetX: placement.offsetX * scale,
    offsetY: placement.offsetY * scale,
  };
}

export function placeChild(
  parent: RenderableBox,
  childSize: { width: number; height: number },
  placement: AlignmentPlacementContract,
): RenderableBox {
  return {
    x: placeAxis(parent.x, parent.width, childSize.width, placement.alignX, placement.offsetX, placement.mode),
    y: placeAxis(parent.y, parent.height, childSize.height, placement.alignY, placement.offsetY, placement.mode),
    width: childSize.width,
    height: childSize.height,
  };
}

function placeAxis(
  parentStart: number,
  parentSize: number,
  childSize: number,
  align: number,
  offset: number,
  mode: "center" | "edge",
) {
  const clamped = Math.max(0, Math.min(1, align));
  if (mode === "center") {
    return parentStart + parentSize * clamped - childSize / 2 + offset;
  }

  const center = parentStart + parentSize / 2 - childSize / 2;
  if (clamped <= 0.5) {
    const outsideStart = parentStart - childSize;
    return lerp(outsideStart, center, clamped / 0.5) + offset;
  }

  const outsideEnd = parentStart + parentSize;
  return lerp(center, outsideEnd, (clamped - 0.5) / 0.5) + offset;
}

function lerp(start: number, end: number, amount: number) {
  return start + (end - start) * amount;
}

export function unionBoxes(boxes: RenderableBox[]): RenderableBox {
  const minX = Math.min(...boxes.map((box) => box.x));
  const minY = Math.min(...boxes.map((box) => box.y));
  const maxX = Math.max(...boxes.map((box) => box.x + box.width));
  const maxY = Math.max(...boxes.map((box) => box.y + box.height));
  return {
    x: minX,
    y: minY,
    width: maxX - minX,
    height: maxY - minY,
  };
}

export function expandBox(box: RenderableBox, padding: number): RenderableBox {
  return {
    x: box.x - padding,
    y: box.y - padding,
    width: box.width + padding * 2,
    height: box.height + padding * 2,
  };
}

export function expandBoxXY(
  box: RenderableBox,
  paddingX: number,
  paddingY: number,
): RenderableBox {
  return {
    x: box.x - paddingX,
    y: box.y - paddingY,
    width: box.width + paddingX * 2,
    height: box.height + paddingY * 2,
  };
}

export function translateBox(box: RenderableBox, origin: { x: number; y: number }): RenderableBox {
  return {
    x: box.x + origin.x,
    y: box.y + origin.y,
    width: box.width,
    height: box.height,
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

export function cssColorWithAlpha(color: string, alpha: number) {
  if (color === "transparent") return color;
  const clamped = Math.max(0, Math.min(1, alpha));
  const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(color.trim());
  if (!match) return color;
  const sourceAlpha = match[2]
    ? Number.parseInt(match[2], 16) / 255
    : 1;
  const resolvedAlpha = Math.max(0, Math.min(1, clamped * sourceAlpha));
  const hex = match[1];
  if (resolvedAlpha >= 1) return `#${hex}`;
  const red = Number.parseInt(hex.slice(0, 2), 16);
  const green = Number.parseInt(hex.slice(2, 4), 16);
  const blue = Number.parseInt(hex.slice(4, 6), 16);
  return `rgba(${red}, ${green}, ${blue}, ${formatAlpha(resolvedAlpha)})`;
}

function formatAlpha(value: number) {
  return Number(value.toFixed(3)).toString();
}

function applyNeutralTint(payload: DesignPreviewPayload, color: string) {
  const root = parseObject(payload.themeTokensJson);
  const tint = asRecord(root.neutralTint);
  const hueDeg = numberValue(tint.hueDeg, 0);
  const saturation = Math.max(0, Math.min(1, numberValue(tint.saturation, 0)));
  if (saturation <= 0) return color;
  const parsed = parseHex(color);
  if (!parsed) return color;
  const [, , lightness] = rgbToHsl(parsed.red / 255, parsed.green / 255, parsed.blue / 255);
  const [red, green, blue] = hslToRgb(normalizeHue(hueDeg) / 360, saturation, lightness);
  const rgb = `#${byteHex(red * 255)}${byteHex(green * 255)}${byteHex(blue * 255)}`;
  return parsed.alpha === 255 ? rgb : `${rgb}${byteHex(parsed.alpha)}`;
}

function parseHex(color: string) {
  const match = /^#([0-9a-f]{6})([0-9a-f]{2})?$/i.exec(color.trim());
  if (!match) return null;
  const hex = match[1];
  return {
    red: Number.parseInt(hex.slice(0, 2), 16),
    green: Number.parseInt(hex.slice(2, 4), 16),
    blue: Number.parseInt(hex.slice(4, 6), 16),
    alpha: match[2] ? Number.parseInt(match[2], 16) : 255,
  };
}

function byteHex(value: number) {
  return Math.max(0, Math.min(255, Math.round(value)))
    .toString(16)
    .padStart(2, "0")
    .toUpperCase();
}

function normalizeHue(value: number) {
  const normalized = value % 360;
  return normalized < 0 ? normalized + 360 : normalized;
}

function rgbToHsl(red: number, green: number, blue: number) {
  const max = Math.max(red, green, blue);
  const min = Math.min(red, green, blue);
  const lightness = (max + min) / 2;
  if (Math.abs(max - min) < 0.000001) return [0, 0, lightness] as const;
  const delta = max - min;
  const saturation =
    lightness > 0.5 ? delta / (2 - max - min) : delta / (max + min);
  const hue =
    max === red
      ? (green - blue) / delta + (green < blue ? 6 : 0)
      : max === green
        ? (blue - red) / delta + 2
        : (red - green) / delta + 4;
  return [hue / 6, saturation, lightness] as const;
}

function hslToRgb(hue: number, saturation: number, lightness: number) {
  if (saturation <= 0) return [lightness, lightness, lightness] as const;
  const q =
    lightness < 0.5
      ? lightness * (1 + saturation)
      : lightness + saturation - lightness * saturation;
  const p = 2 * lightness - q;
  return [
    hueToRgb(p, q, hue + 1 / 3),
    hueToRgb(p, q, hue),
    hueToRgb(p, q, hue - 1 / 3),
  ] as const;
}

function hueToRgb(p: number, q: number, t: number) {
  let value = t;
  if (value < 0) value += 1;
  if (value > 1) value -= 1;
  if (value < 1 / 6) return p + (q - p) * 6 * value;
  if (value < 1 / 2) return q;
  if (value < 2 / 3) return p + (q - p) * (2 / 3 - value) * 6;
  return p;
}
