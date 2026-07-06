import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { iconUriForToken } from "./previewAssetResolver.js";
import {
  numberValue,
  selectedColor,
  stringValue,
} from "./previewColorHelpers.js";
import { renderScale } from "./previewGeometryHelpers.js";
import type {
  NavigationBarDesignContract,
  StatusBarDesignContract,
  SystemBarItemContract,
} from "./systemBarComponentContract.js";

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
