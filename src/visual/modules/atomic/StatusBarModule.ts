import type { ResolvedChatScreenProps } from "../../../domain/schemas/index.js";
import { readNumber, readString } from "../../renderable/helpers.js";
import type { RenderableNode } from "../../renderable/types.js";
import type { VisualModule } from "../types.js";

export interface StatusBarModuleInput {
  frame: number;
  viewport: ResolvedChatScreenProps["viewport"];
  statusBarHeight: number;
  statusBar: ResolvedChatScreenProps["statusBar"];
  tokens: ResolvedChatScreenProps["theme"]["statusBar"];
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function statusBarItems(input: StatusBarModuleInput) {
  const items = Array.isArray(input.statusBar?.items)
    ? input.statusBar.items
    : [];
  return items
    .map((item) => asRecord(item))
    .filter((item) => item.id && item.zone)
    .sort((left, right) => readNumber(left, "order", 0) - readNumber(right, "order", 0));
}

function itemText(item: Record<string, unknown>) {
  const kind = readString(item, "kind", "iconToken");
  const value = item.value;
  if (kind === "text") return String(value ?? "");
  if (kind === "generatedBattery") return `${Math.round(readNumber(item, "value", 0))}%`;
  if (kind === "generatedSignal") return String(readNumber(item, "value", 0));
  return readString(item, "token", readString(item, "label", ""));
}

function renderZone({
  frame,
  foreground,
  gap,
  itemSize,
  items,
  zone,
}: {
  frame: number;
  foreground: string;
  gap: number;
  itemSize: number;
  items: Record<string, unknown>[];
  zone: "left" | "right";
}): RenderableNode {
  return {
    id: `status_bar:${zone}`,
    type: "status_bar_zone",
    role: zone,
    frame,
    style: {
      color: foreground,
      gap,
      itemSize,
      justifyContent: zone === "left" ? "flex-start" : "flex-end",
    },
    children: items
      .filter((item) => readString(item, "zone", "off") === zone)
      .map((item) => {
        const id = readString(item, "id", readString(item, "label", "item"));
        return {
          id: `status_bar:${zone}:${id}`,
          type: "status_bar_item",
          role: readString(item, "kind", "iconToken"),
          frame,
          text: itemText(item),
          style: {
            color: foreground,
            fontSize: itemSize,
            lineHeight: itemSize,
            ...(readString(item, "iconUri", "")
              ? {
                  maskImage: `url("${readString(item, "iconUri", "").replace(/"/g, '\\"')}")`,
                  WebkitMaskImage: `url("${readString(item, "iconUri", "").replace(/"/g, '\\"')}")`,
                }
              : {}),
          },
          metadata: {
            ...item,
          },
        };
      }),
  };
}

export const StatusBarModule: VisualModule<StatusBarModuleInput> = {
  type: "status_bar",
  version: 1,
  render(input) {
    const foreground = readString(input.tokens, "foreground", "#000000");
    const background = readString(input.tokens, "background", "transparent");
    const layout = asRecord(input.statusBar?.layout);
    const itemSize = readNumber(
      layout,
      "itemSize",
      Math.max(10, input.statusBarHeight * 0.34),
    );
    const gap = readNumber(layout, "gap", 6);
    const sidePadding = readNumber(layout, "sidePadding", 24);
    const items = statusBarItems(input);
    return {
      id: "status_bar",
      type: "status_bar",
      role: "device_status",
      frame: input.frame,
      box: {
        x: input.viewport.x,
        y: input.viewport.y,
        width: input.viewport.width,
        height: input.statusBarHeight,
      },
      style: {
        foreground,
        background,
        fontSize: itemSize,
        lineHeight: itemSize,
        gap,
        paddingX: sidePadding,
      },
      children: [
        renderZone({
          frame: input.frame,
          foreground,
          gap,
          itemSize,
          items,
          zone: "left",
        }),
        renderZone({
          frame: input.frame,
          foreground,
          gap,
          itemSize,
          items,
          zone: "right",
        }),
      ],
      metadata: {
        layout: "status_bar_items_v2",
        tokenSource: "themes.status_bar_id -> status_bars.config_json",
        runtimeOverrideSource: "module_instances.behavior_json.statusBar",
      },
    };
  },
};
