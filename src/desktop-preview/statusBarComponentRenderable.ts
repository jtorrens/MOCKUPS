import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { selectedColor, numberValue, stringValue } from "./previewColorHelpers.js";
import { renderScale, previewScreenBox } from "./previewGeometryHelpers.js";
import { iconTokenStyle } from "./previewIconHelpers.js";
import type {
  StatusBarDesignContract,
  StatusBarItemContract,
} from "./statusBarComponentContract.js";

export function statusBarComponentToRenderable(
  payload: DesignPreviewPayload,
  statusBar: StatusBarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const tokens = {
    foreground: selectedColor(payload, "theme.statusBar.foreground"),
    background: selectedColor(payload, "theme.statusBar.background"),
  };
  const layout = {
    height: statusBar.layout.height * scale,
    itemSize: statusBar.layout.itemSize * scale,
    gap: statusBar.layout.gap * scale,
    sidePadding: statusBar.layout.sidePadding * scale,
  };
  const screen = previewScreenBox(payload);
  const box = {
    x: screen.x,
    y: screen.y,
    width: screen.width,
    height: layout.height,
  };
  return {
    id: "status_bar",
    type: "group",
    frame: 0,
    box,
    style: {
      background: tokens.background,
    },
    children: boxedStatusItems(payload, statusBar, layout, tokens.foreground),
  };
}

function boxedStatusItems(
  payload: DesignPreviewPayload,
  statusBar: StatusBarDesignContract,
  layout: { height: number; itemSize: number; gap: number; sidePadding: number },
  foreground: string,
) {
  const { height, itemSize, gap, sidePadding } = layout;
  const y = payload.previewFrame.screenY + (height - itemSize) / 2;

  return (["left", "right"] as const).flatMap((zone) => {
    const zoneItems = statusBar.zones[zone];
    const widths = zoneItems.map((item) => statusItemWidth(item, itemSize));
    const totalWidth = widths.reduce((sum, width) => sum + width, 0)
      + Math.max(0, widths.length - 1) * gap;
    let x = zone === "left"
      ? payload.previewFrame.screenX + sidePadding
      : payload.previewFrame.screenX + payload.previewFrame.screenWidth - sidePadding - totalWidth;

    return zoneItems.map((item, index) => {
      const width = widths[index] ?? itemSize;
      const kind = item.kind || "text";
      const id = item.id || item.label || `item_${index}`;
      const box = { x, y, width, height: itemSize };
      const node = statusBarItemNode(
        `status_bar:${zone}:${id}`,
        kind,
        item,
        box,
        itemSize,
        foreground,
        payload,
      );
      x += width + gap;
      return node;
    });
  });
}

function statusBarItemNode(
  id: string,
  kind: string,
  item: StatusBarItemContract,
  box: RenderableBox,
  itemSize: number,
  foreground: string,
  payload: DesignPreviewPayload,
): RenderableNode {
  if (kind === "generatedBattery") {
    return generatedBatteryRenderable(id, box, foreground, numberValue(item.value, 0), item.charging);
  }
  if (kind === "generatedSignal") {
    return generatedSignalRenderable(id, box, foreground, numberValue(item.value, 0));
  }
  if (kind === "iconToken") {
    return {
      id,
      type: "surface",
      frame: 0,
      text: item.token || item.label,
      box,
      style: iconTokenStyle(payload, item.token, foreground),
    };
  }

  return {
    id,
    type: "text",
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
    frame: 0,
    box,
    children: [
      {
        id: `${id}:body`,
        type: "surface",
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
    frame: 0,
    box,
    children: [1, 2, 3, 4].map((bar) => {
      const height = box.height * (0.24 + bar * 0.16);
      return {
        id: `${id}:bar:${bar}`,
        type: "surface",
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
  };
}

function statusItemWidth(item: StatusBarItemContract, itemSize: number) {
  if (item.kind === "generatedBattery") return itemSize * 1.55;
  if (item.kind === "generatedSignal") return itemSize * 1.08;
  if (item.kind === "iconToken") return itemSize;
  return Math.max(itemSize, stringValue(item.value).length * itemSize * 0.58);
}
