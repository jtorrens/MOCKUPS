import type { RenderableBox } from "../visual/renderable/types.js";
import { interpolateBox } from "./componentRenderableCommon.js";

export interface CollectionLayoutBox {
  id: string;
  box: RenderableBox;
}

/**
 * Interpolates a collection as one layout. Items entering the collection are
 * anchored to the nearest stable neighbour's previous edge so shared gaps are
 * preserved for the complete reflow instead of appearing at their final box.
 */
export function interpolateCollectionReflowBoxes(
  from: CollectionLayoutBox[],
  to: CollectionLayoutBox[],
  progress: number,
) {
  const fromById = new Map(from.map((entry) => [entry.id, entry.box]));
  const stableTargets = to.filter((entry) => fromById.has(entry.id));
  const p = Math.max(0, Math.min(1, progress));
  return new Map(to.map((entry) => {
    const previous = fromById.get(entry.id)
      ?? enteringBox(entry.box, stableTargets, fromById);
    return [entry.id, previous ? interpolateBox(previous, entry.box, p) : entry.box] as const;
  }));
}

function enteringBox(
  box: RenderableBox,
  stableTargets: CollectionLayoutBox[],
  fromById: Map<string, RenderableBox>,
) {
  const anchors = stableTargets.flatMap((target) => {
    const previous = fromById.get(target.id);
    if (!previous) return [];
    return edgeCandidates(box, target.box, previous);
  }).sort((left, right) => left.distance - right.distance);
  return anchors[0]?.box;
}

function edgeCandidates(
  box: RenderableBox,
  targetAnchor: RenderableBox,
  previousAnchor: RenderableBox,
) {
  const epsilon = 0.001;
  const candidates: Array<{ distance: number; box: RenderableBox }> = [];
  const horizontalCenter = previousAnchor.x + previousAnchor.width / 2
    + (box.x + box.width / 2 - (targetAnchor.x + targetAnchor.width / 2));
  const verticalCenter = previousAnchor.y + previousAnchor.height / 2
    + (box.y + box.height / 2 - (targetAnchor.y + targetAnchor.height / 2));
  const horizontalCenterDistance = Math.abs(
    box.x + box.width / 2 - (targetAnchor.x + targetAnchor.width / 2),
  );
  const verticalCenterDistance = Math.abs(
    box.y + box.height / 2 - (targetAnchor.y + targetAnchor.height / 2),
  );
  const belowGap = box.y - (targetAnchor.y + targetAnchor.height);
  if (belowGap >= -epsilon) candidates.push({
    distance: Math.hypot(Math.max(0, belowGap), horizontalCenterDistance),
    box: {
      ...box,
      x: horizontalCenter - box.width / 2,
      y: previousAnchor.y + previousAnchor.height + Math.max(0, belowGap),
    },
  });
  const aboveGap = targetAnchor.y - (box.y + box.height);
  if (aboveGap >= -epsilon) candidates.push({
    distance: Math.hypot(Math.max(0, aboveGap), horizontalCenterDistance),
    box: {
      ...box,
      x: horizontalCenter - box.width / 2,
      y: previousAnchor.y - Math.max(0, aboveGap) - box.height,
    },
  });
  const rightGap = box.x - (targetAnchor.x + targetAnchor.width);
  if (rightGap >= -epsilon) candidates.push({
    distance: Math.hypot(Math.max(0, rightGap), verticalCenterDistance),
    box: {
      ...box,
      x: previousAnchor.x + previousAnchor.width + Math.max(0, rightGap),
      y: verticalCenter - box.height / 2,
    },
  });
  const leftGap = targetAnchor.x - (box.x + box.width);
  if (leftGap >= -epsilon) candidates.push({
    distance: Math.hypot(Math.max(0, leftGap), verticalCenterDistance),
    box: {
      ...box,
      x: previousAnchor.x - Math.max(0, leftGap) - box.width,
      y: verticalCenter - box.height / 2,
    },
  });
  return candidates;
}
