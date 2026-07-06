import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { iconUriForToken } from "./previewAssetResolver.js";
import type {
  NavigationBarDesignContract,
  StatusBarDesignContract,
  SystemBarItemContract,
} from "./systemBarPreviewResolver.js";

type JsonRecord = Record<string, unknown>;

function asRecord(value: unknown): JsonRecord {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as JsonRecord)
    : {};
}

function parseObject(json: string | undefined) {
  return asRecord(JSON.parse(json || "{}"));
}

function renderScale(payload: DesignPreviewPayload) {
  const scale = payload.previewFrame.scaleToPixels;
  return typeof scale === "number" && Number.isFinite(scale) && scale > 0
    ? scale
    : 1;
}

function variants(payload: DesignPreviewPayload) {
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

function colorForMode(
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

function selectedColor(payload: DesignPreviewPayload, token: string, alpha = 1) {
  return colorForMode(payload, token, payload.themeMode || "light", alpha);
}

function numberToken(payload: DesignPreviewPayload, token: string) {
  const raw = tokenValueForMode(payload, token, payload.themeMode || "light");
  const value = numberValue(raw, NaN);
  if (Number.isFinite(value)) return value;
  throw new Error(`Theme token ${token} is not numeric`);
}

function numberValue(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function stringValue(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function requiredNumberValue(value: unknown, path: string) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  throw new Error(`Missing numeric theme value ${path}`);
}

function shadow(payload: DesignPreviewPayload) {
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

function centerBox(payload: DesignPreviewPayload, width: number, height: number) {
  const { previewFrame } = payload;
  return {
    x: previewFrame.screenX + (previewFrame.screenWidth - width) / 2,
    y: previewFrame.screenY + (previewFrame.screenHeight - height) / 2,
    width,
    height,
  };
}

export function statusBarToRenderable(
  payload: DesignPreviewPayload,
  statusBar: StatusBarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const tokens = systemBarTokens(payload, "statusBar");
  const layout = {
    height: statusBar.layout.height * scale,
    itemSize: statusBar.layout.itemSize * scale,
    gap: statusBar.layout.gap * scale,
    sidePadding: statusBar.layout.sidePadding * scale,
  };
  const viewport = designViewport(payload);
  const barHeight = layout.height;
  return {
    id: "status_bar",
    type: "status_bar",
    role: "device_status",
    frame: 0,
    box: {
      x: viewport.x,
      y: viewport.y,
      width: viewport.width,
      height: barHeight,
    },
    style: {
      foreground: tokens.foreground,
      background: tokens.background,
      fontSize: layout.itemSize,
      lineHeight: layout.itemSize,
      gap: layout.gap,
      paddingX: layout.sidePadding,
    },
    children: boxedStatusItems(payload, statusBar, layout, barHeight),
    metadata: {
      route: "system-bar-resolver.web-bridge",
      systemBarType: "statusBar",
    },
  };
}

export function navigationBarToRenderable(
  payload: DesignPreviewPayload,
  navigationBar: NavigationBarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const tokens = systemBarTokens(payload, "navigationBar");
  const layout = {
    height: navigationBar.layout.height * scale,
    itemSize: navigationBar.layout.itemSize * scale,
    sidePadding: navigationBar.layout.sidePadding * scale,
    strokeWidth: navigationBar.layout.strokeWidth * scale,
    cornerRadius: navigationBar.layout.cornerRadius * scale,
    filled: navigationBar.layout.filled,
  };
  const gesture = {
    width: navigationBar.gesture.width * scale,
    height: navigationBar.gesture.height * scale,
    cornerRadius: navigationBar.gesture.cornerRadius * scale,
  };
  const viewport = designViewport(payload);
  const box = {
    x: viewport.x,
    y: viewport.y + viewport.height - layout.height,
    width: viewport.width,
    height: layout.height,
  };
  if (navigationBar.type === "gestureBar") {
    return {
      id: "navigation_bar",
      type: "navigation_bar",
      role: "device_navigation",
      frame: 0,
      box,
      style: {
        background: tokens.background,
      },
      children: [
        {
          id: "navigation_bar:gesture",
          type: "navigation_bar_gesture",
          role: "gesture_bar",
          frame: 0,
          box: {
            x: viewport.x + (viewport.width - gesture.width) / 2,
            y: box.y + (layout.height - gesture.height) / 2,
            width: gesture.width,
            height: gesture.height,
          },
          style: {
            background: tokens.foreground,
            cornerRadius: gesture.cornerRadius,
          },
        },
      ],
      metadata: {
        route: "system-bar-resolver.web-bridge",
        systemBarType: "navigationBar",
      },
    };
  }

  return {
    id: "navigation_bar",
    type: "navigation_bar",
    role: "device_navigation",
    frame: 0,
    box,
    style: {
      foreground: tokens.foreground,
      background: tokens.background,
      fontSize: layout.itemSize,
      lineHeight: layout.itemSize,
      paddingX: layout.sidePadding,
    },
    children: [
      navigationZoneNode(navigationBar, layout, tokens.foreground, "left"),
      navigationZoneNode(navigationBar, layout, tokens.foreground, "center"),
      navigationZoneNode(navigationBar, layout, tokens.foreground, "right"),
    ],
    metadata: {
      route: "system-bar-resolver.web-bridge",
      systemBarType: "navigationBar",
    },
  };
}

function designViewport(payload: DesignPreviewPayload) {
  return {
    x: payload.previewFrame.screenX,
    y: payload.previewFrame.screenY,
    width: payload.previewFrame.screenWidth,
    height: payload.previewFrame.screenHeight,
    safeArea: { top: 0, right: 0, bottom: 0, left: 0 },
  };
}

function systemBarTokens(
  payload: DesignPreviewPayload,
  key: "statusBar" | "navigationBar",
) {
  const prefix = key === "statusBar" ? "theme.statusBar" : "theme.navigationBar";
  return {
    foreground: selectedColor(payload, `${prefix}.foreground`),
    background: selectedColor(payload, `${prefix}.background`),
  };
}

function statusBarItemForRender(
  payload: DesignPreviewPayload,
  item: SystemBarItemContract,
): Record<string, unknown> {
  const iconUri =
    item.kind === "iconToken" && item.token ? iconUriForToken(payload, item.token) : "";
  return iconUri ? { ...item, iconUri } : { ...item };
}

function boxedStatusItems(
  payload: DesignPreviewPayload,
  statusBar: StatusBarDesignContract,
  layout: { itemSize: number; gap: number; sidePadding: number },
  barHeight: number,
) {
  const { itemSize, gap, sidePadding } = layout;
  const foreground = systemBarTokens(payload, "statusBar").foreground;
  const y = payload.previewFrame.screenY + (barHeight - itemSize) / 2;

  return (["left", "right"] as const).flatMap((zone) => {
    const zoneItems = statusBar.zones[zone].map((item) => statusBarItemForRender(payload, item));
    const widths = zoneItems.map((item) => statusItemWidth(item, itemSize));
    const totalWidth = widths.reduce((sum, width) => sum + width, 0)
      + Math.max(0, widths.length - 1) * gap;
    let x = zone === "left"
      ? payload.previewFrame.screenX + sidePadding
      : payload.previewFrame.screenX + payload.previewFrame.screenWidth - sidePadding - totalWidth;

    return zoneItems.map((item, index) => {
      const width = widths[index] ?? itemSize;
      const kind = stringValue(item.kind, "text");
      const id = stringValue(item.id, stringValue(item.label, `item_${index}`));
      const iconUri = stringValue(item.iconUri);
      const node = {
        id: `status_bar:${zone}:${id}`,
        type: "status_bar_item",
        role: kind,
        frame: 0,
        text: kind === "text"
          ? stringValue(item.value)
          : stringValue(item.token, stringValue(item.label)),
        box: { x, y, width, height: itemSize },
        style: {
          color: foreground,
          fontSize: itemSize,
          lineHeight: itemSize,
          ...(iconUri
            ? {
                maskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
                WebkitMaskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
              }
            : {}),
        },
        metadata: { ...item },
      };
      x += width + gap;
      return node;
    });
  });
}

function navigationZoneNode(
  navigationBar: NavigationBarDesignContract,
  layout: {
    itemSize: number;
    strokeWidth: number;
    cornerRadius: number;
    filled: boolean;
  },
  foreground: string,
  zone: "left" | "center" | "right",
): RenderableNode {
  return {
    id: `navigation_bar:${zone}`,
    type: "navigation_bar_zone",
    role: zone,
    frame: 0,
    style: {
      color: foreground,
      itemSize: layout.itemSize,
      justifyContent:
        zone === "left"
          ? "flex-start"
          : zone === "right"
            ? "flex-end"
            : "center",
    },
    children: navigationBar.zones[zone].map((item) => ({
      id: `navigation_bar:${zone}:${item.id || item.label || "item"}`,
      type: "navigation_bar_item",
      role: item.kind || "generatedHome",
      frame: 0,
      style: {
        color: foreground,
        fontSize: layout.itemSize,
        lineHeight: layout.itemSize,
      },
      metadata: {
        ...item,
        filled: layout.filled,
        strokeWidth: layout.strokeWidth,
        cornerRadius: layout.cornerRadius,
      },
    })),
  };
}

function statusItemWidth(item: Record<string, unknown>, itemSize: number) {
  const kind = stringValue(item.kind, "text");
  if (kind === "generatedBattery") return itemSize * 1.55;
  if (kind === "generatedSignal") return itemSize * 1.08;
  if (kind === "iconToken") return itemSize;
  return Math.max(itemSize, stringValue(item.value).length * itemSize * 0.58);
}

function cssColorWithAlpha(color: string, alpha: number) {
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
