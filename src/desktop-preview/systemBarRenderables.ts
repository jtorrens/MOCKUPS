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
    type: "group",
    role: "device_status",
    frame: 0,
    box: {
      x: viewport.x,
      y: viewport.y,
      width: viewport.width,
      height: barHeight,
    },
    style: {
      background: tokens.background,
    },
    children: boxedStatusItems(payload, statusBar, layout, barHeight),
    metadata: {
      route: "system-component.renderable",
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
      type: "group",
      role: "device_navigation",
      frame: 0,
      box,
      style: {
        background: tokens.background,
      },
      children: [
        {
          id: "navigation_bar:gesture",
          type: "surface",
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
        route: "system-component.renderable",
        systemBarType: "navigationBar",
      },
    };
  }

  return {
    id: "navigation_bar",
    type: "group",
    role: "device_navigation",
    frame: 0,
    box,
    style: {
      background: tokens.background,
    },
    children: boxedNavigationItems(box, navigationBar, layout, tokens.foreground),
    metadata: {
      route: "system-component.renderable",
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
      const box = { x, y, width, height: itemSize };
      const node = statusBarItemNode(
        `status_bar:${zone}:${id}`,
        kind,
        item,
        box,
        itemSize,
        foreground,
        iconUri,
      );
      x += width + gap;
      return node;
    });
  });
}

function statusBarItemNode(
  id: string,
  kind: string,
  item: Record<string, unknown>,
  box: RenderableBox,
  itemSize: number,
  foreground: string,
  iconUri: string,
): RenderableNode {
  if (kind === "generatedBattery") {
    return generatedBatteryRenderable(id, box, foreground, numberValue(item.value, 0), item.charging === true);
  }
  if (kind === "generatedSignal") {
    return generatedSignalRenderable(id, box, foreground, numberValue(item.value, 0));
  }
  if (kind === "iconToken") {
    return {
      id,
      type: "surface",
      role: "status_icon",
      frame: 0,
      text: stringValue(item.token, stringValue(item.label)),
      box,
      style: {
        color: foreground,
        ...(iconUri
          ? {
              maskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
              WebkitMaskImage: `url("${iconUri.replace(/"/g, '\\"')}")`,
            }
          : {}),
      },
      metadata: { ...item },
    };
  }

  return {
    id,
    type: "text",
    role: "status_text",
    frame: 0,
    text: stringValue(item.value),
    box,
    style: {
      color: foreground,
      display: "flex",
      alignItems: "center",
      justifyContent: "center",
      fontSize: itemSize,
      lineHeight: itemSize,
      textAlign: "center",
      whiteSpace: "nowrap",
    },
    metadata: { ...item },
  };
}

function generatedBatteryRenderable(
  id: string,
  box: RenderableBox,
  foreground: string,
  rawLevel: number,
  charging: boolean,
): RenderableNode {
  const level = Math.max(0, Math.min(100, rawLevel));
  const bodyWidth = box.height * 1.32;
  const bodyHeight = box.height * 0.62;
  const bodyX = box.x + (box.width - bodyWidth) / 2;
  const bodyY = box.y + (box.height - bodyHeight) / 2;
  const stroke = Math.max(1, box.height * 0.09);
  const innerInset = stroke * 1.35;
  const fillWidth = Math.max(0, (bodyWidth - innerInset * 2) * (level / 100));
  return {
    id,
    type: "group",
    role: "status_battery",
    frame: 0,
    box,
    children: [
      {
        id: `${id}:body`,
        type: "surface",
        role: "battery_body",
        frame: 0,
        box: { x: bodyX, y: bodyY, width: bodyWidth, height: bodyHeight },
        style: {
          borderColor: foreground,
          borderRadius: bodyHeight * 0.18,
          borderWidth: stroke,
        },
      },
      {
        id: `${id}:fill`,
        type: "surface",
        role: "battery_fill",
        frame: 0,
        box: {
          x: bodyX + innerInset,
          y: bodyY + innerInset,
          width: fillWidth,
          height: Math.max(0, bodyHeight - innerInset * 2),
        },
        style: {
          background: foreground,
          borderRadius: bodyHeight * 0.08,
        },
      },
      {
        id: `${id}:cap`,
        type: "surface",
        role: "battery_cap",
        frame: 0,
        box: {
          x: bodyX + bodyWidth + stroke * 0.5,
          y: bodyY + bodyHeight * 0.34,
          width: stroke * 1.45,
          height: bodyHeight * 0.32,
        },
        style: {
          background: foreground,
          borderRadius: stroke,
        },
      },
      ...(charging
        ? [
            {
              id: `${id}:charging`,
              type: "path",
              role: "battery_charging",
              frame: 0,
              box: {
                x: bodyX + bodyWidth * 0.36,
                y: bodyY + bodyHeight * 0.08,
                width: bodyWidth * 0.28,
                height: bodyHeight * 0.84,
              },
              style: {
                fill: "#34c759",
                pathData:
                  "M58 0 L18 48 L45 48 L32 100 L84 38 L56 38 Z",
                viewBox: "0 0 100 100",
              },
            },
          ]
        : []),
    ],
    metadata: { value: level, charging },
  };
}

function generatedSignalRenderable(
  id: string,
  box: RenderableBox,
  foreground: string,
  rawBars: number,
): RenderableNode {
  const bars = Math.max(0, Math.min(4, Math.round(rawBars)));
  const gap = box.width * 0.06;
  const barWidth = (box.width - gap * 3) / 4;
  return {
    id,
    type: "group",
    role: "status_signal",
    frame: 0,
    box,
    children: [1, 2, 3, 4].map((bar) => {
      const height = box.height * (0.24 + bar * 0.16);
      return {
        id: `${id}:bar:${bar}`,
        type: "surface",
        role: "signal_bar",
        frame: 0,
        box: {
          x: box.x + (bar - 1) * (barWidth + gap),
          y: box.y + box.height - height,
          width: barWidth,
          height,
        },
        style: {
          background: foreground,
          borderRadius: Math.max(1, barWidth * 0.32),
          opacity: bar <= bars ? 1 : 0.24,
        },
      };
    }),
    metadata: { value: bars },
  };
}

function boxedNavigationItems(
  barBox: RenderableBox,
  navigationBar: NavigationBarDesignContract,
  layout: {
    itemSize: number;
    sidePadding: number;
    strokeWidth: number;
    cornerRadius: number;
    filled: boolean;
  },
  foreground: string,
): RenderableNode[] {
  return (["left", "center", "right"] as const).flatMap((zone) => {
    const zoneItems = navigationBar.zones[zone];
    const totalWidth =
      zoneItems.length * layout.itemSize +
      Math.max(0, zoneItems.length - 1) * layout.sidePadding * 0.5;
    let x = zone === "left"
      ? barBox.x + layout.sidePadding
      : zone === "right"
        ? barBox.x + barBox.width - layout.sidePadding - totalWidth
        : barBox.x + (barBox.width - totalWidth) / 2;
    const y = barBox.y + (barBox.height - layout.itemSize) / 2;
    return zoneItems.map((item, index) => {
      const id = `navigation_bar:${zone}:${item.id || item.label || index}`;
      const box = { x, y, width: layout.itemSize, height: layout.itemSize };
      x += layout.itemSize + layout.sidePadding * 0.5;
      return navigationButtonNode(id, item, box, layout, foreground);
    });
  });
}

function navigationButtonNode(
  id: string,
  item: SystemBarItemContract,
  box: RenderableBox,
  layout: {
    itemSize: number;
    strokeWidth: number;
    cornerRadius: number;
    filled: boolean;
  },
  foreground: string,
): RenderableNode {
  const role = item.kind || "generatedHome";
  if (role === "generatedBack") {
    return {
      id,
      type: "path",
      role,
      frame: 0,
      box,
      style: {
        color: foreground,
        fill: layout.filled ? "currentColor" : "none",
        pathData: "M64 20 Q64 20 64 20 L28 50 L64 80",
        preserveAspectRatio: "xMidYMid meet",
        stroke: "currentColor",
        strokeLinecap: "round",
        strokeLinejoin: "round",
        strokeWidth: layout.strokeWidth,
        vectorEffect: "non-scaling-stroke",
        viewBox: "0 0 100 100",
      },
      metadata: { ...item },
    };
  }

  return {
    id,
    type: "surface",
    role,
    frame: 0,
    box: role === "generatedHome"
      ? {
          x: box.x + box.width * 0.25,
          y: box.y + box.height * 0.25,
          width: box.width * 0.5,
          height: box.height * 0.5,
        }
      : {
          x: box.x + box.width * 0.28,
          y: box.y + box.height * 0.28,
          width: box.width * 0.44,
          height: box.height * 0.44,
        },
    style: {
      background: layout.filled ? foreground : "transparent",
      borderColor: foreground,
      borderRadius: role === "generatedHome"
        ? box.width
        : layout.cornerRadius,
      borderWidth: layout.strokeWidth,
    },
    metadata: { ...item },
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
