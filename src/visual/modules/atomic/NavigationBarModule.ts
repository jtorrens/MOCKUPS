import type { ResolvedChatScreenProps } from "../../../domain/schemas/index.js";
import { readNumber, readString } from "../../renderable/helpers.js";
import type { RenderableNode } from "../../renderable/types.js";
import type { VisualModule } from "../types.js";

export interface NavigationBarModuleInput {
  frame: number;
  viewport: ResolvedChatScreenProps["viewport"];
  navigationBar: ResolvedChatScreenProps["navigationBar"];
  tokens: ResolvedChatScreenProps["theme"]["navigationBar"];
}

function asRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function navigationBarItems(input: NavigationBarModuleInput) {
  const items = Array.isArray(input.navigationBar?.items)
    ? input.navigationBar.items
    : [];
  return items
    .map((item) => asRecord(item))
    .filter((item) => item.id && item.zone)
    .sort(
      (left, right) =>
        readNumber(left, "order", 0) - readNumber(right, "order", 0),
    );
}

function renderZone({
  frame,
  foreground,
  itemSize,
  items,
  layout,
  zone,
}: {
  frame: number;
  foreground: string;
  itemSize: number;
  items: Record<string, unknown>[];
  layout: Record<string, unknown>;
  zone: "left" | "center" | "right";
}): RenderableNode {
  return {
    id: `navigation_bar:${zone}`,
    type: "navigation_bar_zone",
    role: zone,
    frame,
    style: {
      color: foreground,
      itemSize,
      justifyContent:
        zone === "left"
          ? "flex-start"
          : zone === "right"
            ? "flex-end"
            : "center",
    },
    children: items
      .filter((item) => readString(item, "zone", "off") === zone)
      .map((item) => {
        const id = readString(item, "id", readString(item, "label", "item"));
        return {
          id: `navigation_bar:${zone}:${id}`,
          type: "navigation_bar_item",
          role: readString(item, "kind", "generatedHome"),
          frame,
          style: {
            color: foreground,
            fontSize: itemSize,
            lineHeight: itemSize,
          },
          metadata: {
            ...item,
            filled: layout.filled === true,
            strokeWidth: readNumber(layout, "strokeWidth", 2),
            cornerRadius: readNumber(layout, "cornerRadius", 3),
          },
        };
      }),
  };
}

export const NavigationBarModule: VisualModule<NavigationBarModuleInput> = {
  type: "navigation_bar",
  version: 1,
  render(input) {
    const foreground = readString(input.tokens, "foreground", "#000000");
    const background = readString(input.tokens, "background", "transparent");
    const type = readString(input.navigationBar, "type", "buttons");
    const layout = asRecord(input.navigationBar?.layout);
    const gesture = asRecord(input.navigationBar?.gesture);
    const height = readNumber(layout, "height", 0);
    const itemSize = readNumber(layout, "itemSize", Math.max(10, height * 0.5));
    const sidePadding = readNumber(layout, "sidePadding", 40);
    const items = navigationBarItems(input);
    if (type === "gestureBar") {
      const gestureWidth = readNumber(gesture, "width", 134);
      const gestureHeight = readNumber(gesture, "height", 5);
      const gestureCornerRadius = readNumber(gesture, "cornerRadius", gestureHeight / 2);
      return {
        id: "navigation_bar",
        type: "navigation_bar",
        role: "device_navigation",
        frame: input.frame,
        box: {
          x: input.viewport.x,
          y: input.viewport.y + input.viewport.height - height,
          width: input.viewport.width,
          height,
        },
        style: {
          background,
        },
        children: [
          {
            id: "navigation_bar:gesture",
            type: "navigation_bar_gesture",
            role: "gesture_bar",
            frame: input.frame,
            box: {
              x: input.viewport.x + (input.viewport.width - gestureWidth) / 2,
              y: input.viewport.y + input.viewport.height - height + (height - gestureHeight) / 2,
              width: gestureWidth,
              height: gestureHeight,
            },
            style: {
              background: foreground,
              cornerRadius: gestureCornerRadius,
            },
          },
        ],
        metadata: {
          layout: "navigation_bar_gesture_v1",
          tokenSource: "themes.navigation_bar_id -> navigation_bars.config_json",
        },
      };
    }

    return {
      id: "navigation_bar",
      type: "navigation_bar",
      role: "device_navigation",
      frame: input.frame,
      box: {
        x: input.viewport.x,
        y: input.viewport.y + input.viewport.height - height,
        width: input.viewport.width,
        height,
      },
      style: {
        foreground,
        background,
        fontSize: itemSize,
        lineHeight: itemSize,
        paddingX: sidePadding,
      },
      children: [
        renderZone({
          frame: input.frame,
          foreground,
          itemSize,
          items,
          layout,
          zone: "left",
        }),
        renderZone({
          frame: input.frame,
          foreground,
          itemSize,
          items,
          layout,
          zone: "center",
        }),
        renderZone({
          frame: input.frame,
          foreground,
          itemSize,
          items,
          layout,
          zone: "right",
        }),
      ],
      metadata: {
        layout: "navigation_bar_generated_items_v1",
        tokenSource: "themes.navigation_bar_id -> navigation_bars.config_json",
      },
    };
  },
};
