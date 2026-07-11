import type { RenderableBox, RenderableNode } from "./types.js";

export interface RasterTile {
  x: number;
  y: number;
  width: number;
  height: number;
}

export type RasterFramePlan =
  | { kind: "full"; frame: number; changedNodeIds: string[] }
  | { kind: "hold"; frame: number; sourceFrame: number }
  | {
      kind: "tiles";
      frame: number;
      baseFrame: number;
      changedNodeIds: string[];
      tiles: RasterTile[];
    };

export interface RasterFramePlannerOptions {
  width: number;
  height: number;
  tileSize?: number;
  fullFrameThreshold?: number;
}

interface NodeSnapshot {
  visual: string;
  bounds?: RenderableBox;
}

export function planRasterFrame(
  previous: RenderableNode | undefined,
  current: RenderableNode,
  frame: number,
  options: RasterFramePlannerOptions,
): RasterFramePlan {
  if (!previous) {
    return { kind: "full", frame, changedNodeIds: flattenNodeIds(current) };
  }

  const previousNodes = snapshotNodes(previous);
  const currentNodes = snapshotNodes(current);
  const changedNodeIds = [...new Set([...previousNodes.keys(), ...currentNodes.keys()])]
    .filter((id) => previousNodes.get(id)?.visual !== currentNodes.get(id)?.visual)
    .sort();
  if (changedNodeIds.length === 0) {
    return { kind: "hold", frame, sourceFrame: frame - 1 };
  }

  const dirtyBounds = changedNodeIds.flatMap((id) => [
    previousNodes.get(id)?.bounds,
    currentNodes.get(id)?.bounds,
  ]).filter((box): box is RenderableBox => box !== undefined);
  const tiles = tilesForBounds(dirtyBounds, options);
  const dirtyArea = tiles.reduce((sum, tile) => sum + tile.width * tile.height, 0);
  const fullArea = Math.max(1, options.width * options.height);
  if (tiles.length === 0 || dirtyArea / fullArea >= (options.fullFrameThreshold ?? 0.45)) {
    return { kind: "full", frame, changedNodeIds };
  }

  return {
    kind: "tiles",
    frame,
    baseFrame: frame - 1,
    changedNodeIds,
    tiles,
  };
}

function snapshotNodes(root: RenderableNode): Map<string, NodeSnapshot> {
  const result = new Map<string, NodeSnapshot>();
  const visit = (node: RenderableNode) => {
    const { children: _children, frame: _frame, ...visual } = node;
    result.set(node.id, {
      visual: stableStringify(visual),
      bounds: node.box,
    });
    node.children?.forEach(visit);
  };
  visit(root);
  return result;
}

function flattenNodeIds(root: RenderableNode): string[] {
  return [root.id, ...(root.children?.flatMap(flattenNodeIds) ?? [])].sort();
}

function tilesForBounds(
  bounds: RenderableBox[],
  options: RasterFramePlannerOptions,
): RasterTile[] {
  const tileSize = Math.max(16, Math.round(options.tileSize ?? 128));
  const keys = new Set<string>();
  for (const box of bounds) {
    const left = clamp(Math.floor(box.x / tileSize), 0, Math.ceil(options.width / tileSize));
    const top = clamp(Math.floor(box.y / tileSize), 0, Math.ceil(options.height / tileSize));
    const right = clamp(Math.ceil((box.x + box.width) / tileSize), 0, Math.ceil(options.width / tileSize));
    const bottom = clamp(Math.ceil((box.y + box.height) / tileSize), 0, Math.ceil(options.height / tileSize));
    for (let y = top; y < bottom; y += 1) {
      for (let x = left; x < right; x += 1) keys.add(`${x}:${y}`);
    }
  }
  return [...keys].sort((left, right) => {
    const [leftX, leftY] = left.split(":").map(Number);
    const [rightX, rightY] = right.split(":").map(Number);
    return leftY - rightY || leftX - rightX;
  }).map((key) => {
    const [column, row] = key.split(":").map(Number);
    const x = column * tileSize;
    const y = row * tileSize;
    return {
      x,
      y,
      width: Math.min(tileSize, options.width - x),
      height: Math.min(tileSize, options.height - y),
    };
  });
}

function stableStringify(value: unknown): string {
  if (Array.isArray(value)) return `[${value.map(stableStringify).join(",")}]`;
  if (value && typeof value === "object") {
    return `{${Object.entries(value as Record<string, unknown>)
      .sort(([left], [right]) => left.localeCompare(right))
      .map(([key, entry]) => `${JSON.stringify(key)}:${stableStringify(entry)}`)
      .join(",")}}`;
  }
  return JSON.stringify(value) ?? "undefined";
}

function clamp(value: number, minimum: number, maximum: number) {
  return Math.max(minimum, Math.min(maximum, value));
}
