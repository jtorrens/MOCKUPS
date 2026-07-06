import type { RenderableBox } from "../visual/renderable/types.js";
import type { AlignmentPlacementContract } from "./previewComponentContracts.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function renderScale(payload: DesignPreviewPayload) {
  const scale = payload.previewFrame.scaleToPixels;
  return typeof scale === "number" && Number.isFinite(scale) && scale > 0
    ? scale
    : 1;
}

export function centerBox(payload: DesignPreviewPayload, width: number, height: number) {
  const { previewFrame } = payload;
  return {
    x: previewFrame.screenX + (previewFrame.screenWidth - width) / 2,
    y: previewFrame.screenY + (previewFrame.screenHeight - height) / 2,
    width,
    height,
  };
}

export function boundedCenterBox(
  payload: DesignPreviewPayload,
  width: number,
  height: number,
) {
  const centered = centerBox(payload, width, height);
  const minX = payload.previewFrame.screenX;
  const minY = payload.previewFrame.screenY;
  const maxX = payload.previewFrame.screenX + payload.previewFrame.screenWidth - width;
  const maxY = payload.previewFrame.screenY + payload.previewFrame.screenHeight - height;
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
