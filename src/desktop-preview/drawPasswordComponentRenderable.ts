import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { boundedCenterBox, numberToken, renderScale, selectedColor } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { DrawPasswordDesignContract } from "./drawPasswordComponentContract.js";

export function drawPasswordComponentToRenderable(payload: DesignPreviewPayload, draw: DrawPasswordDesignContract) {
  const size = measureDrawPasswordComponent(payload, draw);
  return drawPasswordComponentToRenderableAt(payload, draw, boundedCenterBox(payload, size.width, size.height));
}

export function measureDrawPasswordComponent(payload: DesignPreviewPayload, draw: DrawPasswordDesignContract) {
  const scale = renderScale(payload);
  const nodeSize = Math.max(1, draw.nodeSize * scale);
  const columnGap = Math.max(0, numberToken(payload, draw.columnGapToken) * scale);
  const rowGap = Math.max(0, numberToken(payload, draw.rowGapToken) * scale);
  return {
    width: nodeSize * draw.grid.columns + columnGap * Math.max(0, draw.grid.columns - 1),
    height: nodeSize * draw.grid.rows + rowGap * Math.max(0, draw.grid.rows - 1),
  };
}

export function drawPasswordComponentToRenderableAt(
  payload: DesignPreviewPayload,
  draw: DrawPasswordDesignContract,
  box: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const nodeSize = Math.max(1, draw.nodeSize * scale);
  const columnGap = Math.max(0, numberToken(payload, draw.columnGapToken) * scale);
  const rowGap = Math.max(0, numberToken(payload, draw.rowGapToken) * scale);
  const nodeColor = selectedColor(payload, draw.nodeColorToken);
  const lineColor = selectedColor(payload, draw.lineColorToken);
  const points = draw.pattern.slice(0, draw.visibleCount).map((node) => {
    const index = node - 1;
    const column = index % draw.grid.columns;
    const row = Math.floor(index / draw.grid.columns);
    return {
      x: column * (nodeSize + columnGap) + nodeSize / 2,
      y: row * (nodeSize + rowGap) + nodeSize / 2,
    };
  });
  const pathData = points.map((point, index) => `${index === 0 ? "M" : "L"}${point.x} ${point.y}`).join(" ");
  const children: RenderableNode[] = [];
  if (pathData) {
    children.push({
      id: `${draw.id}.path`, type: "path", frame: 0, box,
      style: { fill: "none", stroke: lineColor, strokeWidth: Math.max(1, draw.lineWidth * scale), strokeLinecap: "round", strokeLinejoin: "round", pathData, viewBox: `0 0 ${box.width} ${box.height}`, preserveAspectRatio: "none" },
    });
  }
  for (let index = 0; index < draw.grid.columns * draw.grid.rows; index += 1) {
    const column = index % draw.grid.columns;
    const row = Math.floor(index / draw.grid.columns);
    const selected = points.some((point) => point.x === column * (nodeSize + columnGap) + nodeSize / 2 && point.y === row * (nodeSize + rowGap) + nodeSize / 2);
    children.push({
      id: `${draw.id}.node.${index + 1}`, type: "surface", frame: 0,
      box: { x: box.x + column * (nodeSize + columnGap), y: box.y + row * (nodeSize + rowGap), width: nodeSize, height: nodeSize },
      style: { background: selected ? nodeColor : "transparent", borderColor: nodeColor, borderRadius: nodeSize / 2, borderWidth: Math.max(1, scale) },
    });
  }
  return { id: draw.id, type: "group", frame: 0, box, style: { overflow: "visible" }, children };
}
