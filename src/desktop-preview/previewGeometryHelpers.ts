import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { AlignmentPlacementContract } from "./previewComponentContracts.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export const referenceDesignWidth = 360;

export function renderScale(payload: DesignPreviewPayload) {
  return payload.previewFrame.screenWidth / referenceDesignWidth;
}

export function screenPercentToDesignWidth(
  payload: DesignPreviewPayload,
  percent: number,
) {
  const scale = renderScale(payload);
  const screenDesignWidth = payload.previewFrame.screenWidth / scale;
  return Math.max(1, screenDesignWidth * (percent / 100));
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

export function previewScreenBox(payload: DesignPreviewPayload): RenderableBox {
  return {
    x: payload.previewFrame.screenX,
    y: payload.previewFrame.screenY,
    width: payload.previewFrame.screenWidth,
    height: payload.previewFrame.screenHeight,
  };
}

export function rootPreviewScreenBox(payload: DesignPreviewPayload): RenderableBox {
  const frame = payload.rootPreviewFrame ?? payload.previewFrame;
  return {
    x: frame.screenX,
    y: frame.screenY,
    width: frame.screenWidth,
    height: frame.screenHeight,
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
  mode: "center" | "insideEdge" | "outsideEdge",
) {
  const clamped = Math.max(0, Math.min(1, align));
  if (mode === "center") {
    return parentStart + parentSize * clamped - childSize / 2 + offset;
  }
  if (mode === "insideEdge") {
    return parentStart + (parentSize - childSize) * clamped + offset;
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

export function renderableVisualBounds(node: RenderableNode): RenderableBox {
  const boxes: RenderableBox[] = [];
  collectRenderableBoxes(node, boxes);
  if (boxes.length === 0) {
    return { x: 0, y: 0, width: 0, height: 0 };
  }
  return boxes.length === 1 ? boxes[0]! : unionBoxes(boxes);
}

function collectRenderableBoxes(node: RenderableNode, boxes: RenderableBox[]) {
  if (node.box) {
    boxes.push(node.box);
  }
  for (const child of node.children ?? []) {
    collectRenderableBoxes(child, boxes);
  }
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

export function boxEdgeIntrusionInsets(
  container: RenderableBox,
  child: RenderableBox | undefined,
) {
  if (!child) {
    return { left: 0, top: 0, right: 0, bottom: 0 };
  }

  const containerRight = container.x + container.width;
  const containerBottom = container.y + container.height;
  const childRight = child.x + child.width;
  const childBottom = child.y + child.height;
  return {
    left: child.x < container.x && childRight > container.x
      ? childRight - container.x
      : 0,
    top: child.y < container.y && childBottom > container.y
      ? childBottom - container.y
      : 0,
    right: child.x < containerRight && childRight > containerRight
      ? containerRight - child.x
      : 0,
    bottom: child.y < containerBottom && childBottom > containerBottom
      ? containerBottom - child.y
      : 0,
  };
}

/**
 * Returns the content space occupied by a child anchored at a container edge.
 * Unlike `boxEdgeIntrusionInsets`, the child may be completely inside the
 * container. A centred child reserves no edge because it does not establish an
 * edge-owned content boundary.
 */
export function boxEdgeReservationInsets(
  container: RenderableBox,
  child: RenderableBox | undefined,
) {
  if (!child) {
    return { left: 0, top: 0, right: 0, bottom: 0 };
  }

  const containerRight = container.x + container.width;
  const containerBottom = container.y + container.height;
  const childRight = child.x + child.width;
  const childBottom = child.y + child.height;
  const overlapsX = childRight > container.x && child.x < containerRight;
  const overlapsY = childBottom > container.y && child.y < containerBottom;
  const childCenterX = child.x + child.width / 2;
  const childCenterY = child.y + child.height / 2;
  const containerCenterX = container.x + container.width / 2;
  const containerCenterY = container.y + container.height / 2;
  return {
    left: overlapsX && childCenterX < containerCenterX
      ? Math.max(0, Math.min(containerRight, childRight) - container.x)
      : 0,
    top: overlapsY && childCenterY < containerCenterY
      ? Math.max(0, Math.min(containerBottom, childBottom) - container.y)
      : 0,
    right: overlapsX && childCenterX > containerCenterX
      ? Math.max(0, containerRight - Math.max(container.x, child.x))
      : 0,
    bottom: overlapsY && childCenterY > containerCenterY
      ? Math.max(0, containerBottom - Math.max(container.y, child.y))
      : 0,
  };
}

/**
 * Computes the minimum box dimensions required to keep a child placed with an
 * interior alignment inside its parent. Outside-edge placement deliberately
 * remains overflow and therefore does not enlarge its parent.
 */
export function minimumContainerSizeForPlacedChild(
  container: RenderableBox,
  childSize: { width: number; height: number } | undefined,
  placement: AlignmentPlacementContract,
) {
  if (!childSize || placement.mode === "outsideEdge") {
    return { width: container.width, height: container.height };
  }
  return {
    width: minimumAxisSizeForPlacedChild(
      container.width,
      childSize.width,
      placement.alignX,
      placement.offsetX,
      placement.mode,
    ),
    height: minimumAxisSizeForPlacedChild(
      container.height,
      childSize.height,
      placement.alignY,
      placement.offsetY,
      placement.mode,
    ),
  };
}

function minimumAxisSizeForPlacedChild(
  currentSize: number,
  childSize: number,
  alignment: number,
  offset: number,
  mode: "center" | "insideEdge",
) {
  const align = Math.max(0, Math.min(1, alignment));
  const startInset = mode === "center" ? childSize / 2 : childSize;
  const endInset = mode === "center" ? childSize / 2 : 0;
  const minimumAtStart = align > 0
    ? (startInset - offset) / align
    : Number.NEGATIVE_INFINITY;
  const minimumAtEnd = align < 1
    ? (endInset + offset) / (1 - align)
    : Number.NEGATIVE_INFINITY;
  return Math.max(currentSize, childSize, minimumAtStart, minimumAtEnd);
}

export function translateBox(box: RenderableBox, origin: { x: number; y: number }): RenderableBox {
  return {
    x: box.x + origin.x,
    y: box.y + origin.y,
    width: box.width,
    height: box.height,
  };
}

export function translateRenderableNode(
  node: RenderableNode,
  origin: { x: number; y: number },
  boxOverride?: RenderableBox,
): RenderableNode {
  const translationFactor = node.style?.rootOverlay === true
    ? rootOverlayTranslationFactor(node.style.rootOverlayTranslationFactor)
    : 1;
  const effectiveOrigin = {
    x: origin.x * translationFactor,
    y: origin.y * translationFactor,
  };
  return {
    ...node,
    box: boxOverride ?? (node.box ? translateBox(node.box, effectiveOrigin) : undefined),
    children: node.children?.map((child) => translateRenderableNode(child, effectiveOrigin)),
  };
}

function rootOverlayTranslationFactor(value: unknown) {
  if (typeof value !== "number" || !Number.isFinite(value)) return 0;
  return Math.max(0, Math.min(1, value));
}

export function interpolateBox(
  from: RenderableBox,
  to: RenderableBox,
  progress: number,
): RenderableBox {
  const p = Math.max(0, Math.min(1, progress));
  return {
    x: lerp(from.x, to.x, p),
    y: lerp(from.y, to.y, p),
    width: lerp(from.width, to.width, p),
    height: lerp(from.height, to.height, p),
  };
}

export function interpolateRenderableGeometry(
  from: RenderableNode,
  to: RenderableNode,
  progress: number,
): RenderableNode {
  const previousChildren = new Map((from.children ?? []).map((child) => [child.id, child]));
  return {
    ...to,
    box: from.box && to.box ? interpolateBox(from.box, to.box, progress) : to.box,
    children: to.children?.map((child) => {
      const previous = previousChildren.get(child.id);
      return previous ? interpolateRenderableGeometry(previous, child, progress) : child;
    }),
  };
}
