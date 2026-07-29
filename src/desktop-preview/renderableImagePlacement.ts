import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { numberValue, stringValue } from "./previewValueHelpers.js";

export function renderableImagePaintBox(node: RenderableNode): RenderableBox | undefined {
  const box = node.box;
  if (!box) return undefined;

  const scale = Math.max(0.01, finite(node.metadata?.imageScale) ?? 1);
  const baseSize = Math.max(1, finite(node.metadata?.imageBaseSize) ?? box.width);
  const offsetX = ((finite(node.metadata?.imageOffsetX) ?? 0) / baseSize) * box.width;
  const offsetY = ((finite(node.metadata?.imageOffsetY) ?? 0) / baseSize) * box.width;
  const intrinsicWidth = finite(node.metadata?.imageIntrinsicWidth);
  const intrinsicHeight = finite(node.metadata?.imageIntrinsicHeight);

  if (!intrinsicWidth || !intrinsicHeight) {
    return {
      x: box.x + (box.width - box.width * scale) / 2 + offsetX,
      y: box.y + (box.height - box.height * scale) / 2 + offsetY,
      width: box.width * scale,
      height: box.height * scale,
    };
  }

  const fit = stringValue(node.style?.objectFit, "cover");
  const baseScale = fit === "contain"
    ? Math.min(box.width / intrinsicWidth, box.height / intrinsicHeight)
    : Math.max(box.width / intrinsicWidth, box.height / intrinsicHeight);
  const width = intrinsicWidth * baseScale * scale;
  const height = intrinsicHeight * baseScale * scale;
  const desiredX = box.x + (box.width - width) / 2 + offsetX;
  const desiredY = box.y + (box.height - height) / 2 + offsetY;

  return {
    x: fit === "cover" ? clampedCoverOrigin(desiredX, box.x, box.width, width) : desiredX,
    y: fit === "cover" ? clampedCoverOrigin(desiredY, box.y, box.height, height) : desiredY,
    width,
    height,
  };
}

function clampedCoverOrigin(
  desired: number,
  viewportOrigin: number,
  viewportSize: number,
  paintedSize: number,
) {
  if (paintedSize < viewportSize) {
    return viewportOrigin + (viewportSize - paintedSize) / 2;
  }
  return Math.max(
    viewportOrigin + viewportSize - paintedSize,
    Math.min(viewportOrigin, desired),
  );
}

function finite(value: unknown) {
  const resolved = numberValue(value, NaN);
  return Number.isFinite(resolved) ? resolved : undefined;
}
