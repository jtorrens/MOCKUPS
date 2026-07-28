import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  numberToken,
  previewScreenBox,
  renderScale,
} from "./componentRenderableCommon.js";
import { buttonComponentToRenderableAt, measureButtonComponent } from "./buttonComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import { renderAuthoringCollectionItem } from "./previewAuthoringTarget.js";

export function measureIconRowComponent(payload: DesignPreviewPayload, iconRow: IconRowDesignContract) {
  const sizes = iconRow.items.map((item) => measureButtonComponent(payload, item.button));
  const gap = Math.max(0, numberToken(payload, iconRow.gapToken) * renderScale(payload));
  const gaps = Math.max(0, sizes.length - 1) * gap;
  return iconRow.orientation === "vertical"
    ? {
        width: sizes.length ? Math.max(...sizes.map((size) => size.width)) : 0,
        height: sizes.reduce((sum, size) => sum + size.height, 0) + gaps,
        sizes,
        gap,
      }
    : {
        width: sizes.reduce((sum, size) => sum + size.width, 0) + gaps,
        height: sizes.length ? Math.max(...sizes.map((size) => size.height)) : 0,
        sizes,
        gap,
      };
}

export function iconRowComponentToRenderable(payload: DesignPreviewPayload, iconRow: IconRowDesignContract): RenderableNode {
  const size = measureIconRowComponent(payload, iconRow);
  const screen = previewScreenBox(payload);
  const assignedSize = iconRowAssignedSize(iconRow, size, screen);
  return iconRowComponentToRenderableAt(
    payload,
    iconRow,
    boundedCenterBox(payload, assignedSize.width, assignedSize.height),
  );
}

export function iconRowComponentToRenderableAt(
  payload: DesignPreviewPayload,
  iconRow: IconRowDesignContract,
  box: RenderableBox,
): RenderableNode {
  const metrics = measureIconRowComponent(payload, iconRow);
  const itemSizes = distributedItemSizes(iconRow, box, metrics);
  let cursor = 0;
  const children = iconRow.items.map((item, index) => {
    const size = itemSizes[index] ?? { width: 0, height: 0 };
    const buttonBox = iconRow.orientation === "vertical"
      ? { x: box.x + (box.width - size.width) * 0.5, y: box.y + cursor, width: size.width, height: size.height }
      : { x: box.x + cursor, y: box.y + (box.height - size.height) * 0.5, width: size.width, height: size.height };
    cursor += (iconRow.orientation === "vertical" ? size.height : size.width) + metrics.gap;
    return renderAuthoringCollectionItem(
      payload,
      "component.iconRow",
      "component.iconRow.items",
      item.id,
      (itemPayload) => buttonComponentToRenderableAt(
        itemPayload,
        item.button,
        buttonBox,
      ),
    );
  });
  return { id: iconRow.id, type: "group", frame: 0, box, style: { overflow: "hidden" }, children };
}

export function iconRowAssignedSize(
  iconRow: IconRowDesignContract,
  intrinsicSize: { width: number; height: number },
  availableBox: RenderableBox,
) {
  if (iconRow.itemSizingMode === "content") return intrinsicSize;
  return iconRow.orientation === "vertical"
    ? { width: intrinsicSize.width, height: availableBox.height }
    : { width: availableBox.width, height: intrinsicSize.height };
}

function distributedItemSizes(
  iconRow: IconRowDesignContract,
  box: RenderableBox,
  metrics: ReturnType<typeof measureIconRowComponent>,
) {
  if (iconRow.itemSizingMode === "content" || metrics.sizes.length === 0) {
    return metrics.sizes;
  }
  const mainSize = iconRow.orientation === "vertical" ? box.height : box.width;
  const gaps = Math.max(0, metrics.sizes.length - 1) * metrics.gap;
  const available = Math.max(0, mainSize - gaps);
  const itemMainSize = available / metrics.sizes.length;
  return metrics.sizes.map((size) => iconRow.orientation === "vertical"
    ? { width: size.width, height: itemMainSize }
    : { width: itemMainSize, height: size.height });
}
