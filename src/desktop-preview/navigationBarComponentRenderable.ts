import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type {
  NavigationBarDesignContract,
  NavigationBarItemContract,
} from "./navigationBarComponentContract.js";
import { selectedColor } from "./previewColorHelpers.js";
import { renderScale, previewScreenBox } from "./previewGeometryHelpers.js";
import { numberToken } from "./componentRenderableCommon.js";

export function navigationBarComponentToRenderable(
  payload: DesignPreviewPayload,
  navigationBar: NavigationBarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const tokens = {
    foreground: selectedColor(payload, navigationBar.foregroundColorToken),
    background: selectedColor(
      payload,
      navigationBar.backgroundColorToken,
      navigationBar.backgroundAlpha,
    ),
  };
  const layout = {
    height: navigationBar.layout.height * scale,
    itemSize: navigationBar.layout.itemSize * scale,
    sidePadding: numberToken(payload, navigationBar.layout.sidePaddingToken) * scale,
    strokeWidth: navigationBar.layout.strokeWidth * scale,
    cornerRadius: navigationBar.layout.cornerRadius * scale,
    filled: navigationBar.layout.filled,
  };
  const gesture = {
    width: navigationBar.gesture.width * scale,
    height: navigationBar.gesture.height * scale,
    cornerRadius: navigationBar.gesture.cornerRadius * scale,
  };
  const screen = previewScreenBox(payload);
  const box = {
    x: screen.x,
    y: screen.y + screen.height - layout.height,
    width: screen.width,
    height: layout.height,
  };
  if (navigationBar.type === "gestureBar") {
    return {
      id: "navigation_bar",
      type: "group",
      frame: 0,
      box,
      style: {
        background: tokens.background,
      },
      children: [
        {
          id: "navigation_bar:gesture",
          type: "surface",
          frame: 0,
          box: {
            x: screen.x + (screen.width - gesture.width) / 2,
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
    frame: 0,
    box,
    style: {
      background: tokens.background,
    },
    children: boxedNavigationItems(box, navigationBar, layout, tokens.foreground),
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
    return zoneItems.map((item) => {
      const id = `navigation_bar:${zone}:${item.id}`;
      const box = { x, y, width: layout.itemSize, height: layout.itemSize };
      x += layout.itemSize + layout.sidePadding * 0.5;
      return navigationButtonNode(id, item, box, layout, foreground);
    });
  });
}

function navigationButtonNode(
  id: string,
  item: NavigationBarItemContract,
  box: RenderableBox,
  layout: {
    itemSize: number;
    strokeWidth: number;
    cornerRadius: number;
    filled: boolean;
  },
  foreground: string,
): RenderableNode {
  const kind = item.kind;
  if (kind === "generatedBack") {
    return {
      id,
      type: "path",
      frame: 0,
      box,
      style: {
        color: foreground,
        fill: layout.filled ? "currentColor" : "none",
        pathData: "M64 20 L28 50 L64 80 Z",
        preserveAspectRatio: "xMidYMid meet",
        stroke: "currentColor",
        strokeLinecap: "round",
        strokeLinejoin: "round",
        strokeWidth: layout.strokeWidth,
        vectorEffect: "non-scaling-stroke",
        viewBox: "0 0 100 100",
      },
    };
  }

  return {
    id,
    type: "surface",
    frame: 0,
    box: kind === "generatedHome"
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
      borderRadius: kind === "generatedHome"
        ? box.width
        : layout.cornerRadius,
      borderWidth: layout.strokeWidth,
    },
  };
}
