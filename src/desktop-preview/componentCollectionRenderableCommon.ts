import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type {
  ComponentCollectionChildRenderer,
  ComponentCollectionItemContract,
  ComponentCollectionLayoutItem,
  ComponentCollectionSizingMode,
} from "./componentCollectionContract.js";
import { boundedCenterBox, embeddedComponentPayload, interpolateBox, interpolateRenderableGeometry, numberToken, previewScreenBox, renderScale, translateRenderableNode } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { wrapExitMotionFrame, wrapMotionFrame } from "./previewMotionHelpers.js";

interface MeasuredItem {
  item: ComponentCollectionLayoutItem;
  node: RenderableNode;
  box: RenderableBox;
  fixedGapBefore: number;
}

interface FlowOptions {
  id: string;
  sizingMode: ComponentCollectionSizingMode;
  startGapToken: string;
  endGapToken: string;
  itemSizingMode?: "intrinsic" | "largest";
}

interface StackedOptions extends FlowOptions {
  direction: "down" | "up";
  offsetToken: string;
  scaleRatio: number;
  opacityRatio: number;
}

export function renderComponentCollectionFlow(
  payload: DesignPreviewPayload,
  items: ComponentCollectionItemContract[],
  renderChild: ComponentCollectionChildRenderer,
  options: FlowOptions,
): RenderableNode {
  if (options.itemSizingMode === "largest") {
    return renderMeasuredFlow(payload, measureItems(payload, items, renderChild, "largest"), options);
  }
  return renderComponentCollectionFlowResolved(
    payload,
    items,
    (item) => {
      const component = item as ComponentCollectionItemContract;
      return renderChild(embeddedComponentPayload(payload, component.componentType, component.config, component.inputs));
    },
    options,
  );
}

export function renderComponentCollectionFlowResolved(
  payload: DesignPreviewPayload,
  items: ComponentCollectionLayoutItem[],
  resolveNode: (item: ComponentCollectionLayoutItem) => RenderableNode,
  options: FlowOptions,
): RenderableNode {
  const measured = measureResolvedItems(payload, items, resolveNode);
  return renderMeasuredFlow(payload, measured, options);
}

function renderMeasuredFlow(
  payload: DesignPreviewPayload,
  measured: MeasuredItem[],
  options: FlowOptions,
): RenderableNode {
  const scale = renderScale(payload);
  const startGap = Math.max(0, numberToken(payload, options.startGapToken) * scale);
  const endGap = Math.max(0, numberToken(payload, options.endGapToken) * scale);
  const naturalWidth = measured.length ? Math.max(...measured.map(({ box }) => box.width)) : 0;
  const naturalHeight = startGap + endGap + measured.reduce((sum, current, index) =>
    sum + current.box.height + (index > 0 ? current.fixedGapBefore : 0), 0);
  const parentBox = options.sizingMode === "fill"
    ? previewScreenBox(payload)
    : boundedCenterBox(payload, naturalWidth, naturalHeight);
  const totalWeight = measured.slice(1).reduce((sum, current) =>
    sum + (current.item.gapBeforeMode === "reflow" ? current.item.gapBeforeWeight : 0), 0);
  const reflowSpace = options.sizingMode === "fill" ? Math.max(0, parentBox.height - naturalHeight) : 0;
  let cursorY = parentBox.y + startGap;
  const children = measured.map((current, index) => {
    if (index > 0) {
      cursorY += current.item.gapBeforeMode === "fixed"
        ? current.fixedGapBefore
        : totalWeight > 0 ? reflowSpace * current.item.gapBeforeWeight / totalWeight : 0;
    }
    const x = alignedX(parentBox, current.box, current.item.alignment);
    const translated = translateRenderableNode(current.node, {
      x: x - current.box.x,
      y: cursorY - current.box.y,
    });
    cursorY += current.box.height;
    return translated;
  });
  return collectionGroup(options.id, parentBox, children);
}

export function renderComponentCollectionStacked(
  payload: DesignPreviewPayload,
  items: ComponentCollectionItemContract[],
  renderChild: ComponentCollectionChildRenderer,
  options: StackedOptions,
): RenderableNode {
  const scale = renderScale(payload);
  const startGap = Math.max(0, numberToken(payload, options.startGapToken) * scale);
  const endGap = Math.max(0, numberToken(payload, options.endGapToken) * scale);
  const offset = Math.max(0, numberToken(payload, options.offsetToken) * scale);
  const measured = measureItems(payload, items, renderChild, options.itemSizingMode ?? "intrinsic");
  const naturalWidth = measured.length ? Math.max(...measured.map(({ box }) => box.width)) : 0;
  const naturalHeight = measured.length
    ? startGap + endGap + Math.max(...measured.map(({ box }, index) => box.height + index * offset))
    : startGap + endGap;
  const parentBox = options.sizingMode === "fill"
    ? previewScreenBox(payload)
    : boundedCenterBox(payload, naturalWidth, naturalHeight);
  const children = measured.map((current, index) => {
    const x = alignedX(parentBox, current.box, current.item.alignment);
    const y = options.direction === "down"
      ? parentBox.y + startGap + index * offset
      : parentBox.y + parentBox.height - endGap - current.box.height - index * offset;
    const translated = translateRenderableNode(current.node, {
      x: x - current.box.x,
      y: y - current.box.y,
    });
    const depthScale = Math.pow(options.scaleRatio, index);
    const depthOpacity = Math.pow(options.opacityRatio, index);
    return {
      ...translated,
      transform: {
        ...translated.transform,
        scale: (translated.transform?.scale ?? 1) * depthScale,
        opacity: Math.max(0, Math.min(1, (translated.transform?.opacity ?? 1) * depthOpacity)),
      },
    };
  }).reverse();
  return collectionGroup(options.id, parentBox, children);
}

export function interpolateComponentCollectionReflow(
  from: RenderableNode,
  fromItems: ComponentCollectionLayoutItem[],
  to: RenderableNode,
  toItems: ComponentCollectionLayoutItem[],
  progress: number,
) {
  const fromChildren = from.children ?? [];
  const toChildren = to.children ?? [];
  const fromById = new Map(fromItems.map((item, index) => [item.id, fromChildren[index]]));
  const p = Math.max(0, Math.min(1, progress));
  return {
    ...to,
    box: from.box && to.box ? interpolateBox(from.box, to.box, p) : to.box,
    children: toItems.map((item, index) => {
      const node = toChildren[index];
      const previous = fromById.get(item.id);
      return node && previous ? interpolateRenderableGeometry(previous, node, p) : node;
    }).filter((node): node is RenderableNode => node !== undefined),
  };
}

function measureItems(
  payload: DesignPreviewPayload,
  items: ComponentCollectionItemContract[],
  renderChild: ComponentCollectionChildRenderer,
  itemSizingMode: "intrinsic" | "largest",
): MeasuredItem[] {
  const scale = renderScale(payload);
  const intrinsic = items.map((item) => {
    const resolved = renderChild(embeddedComponentPayload(payload, item.componentType, item.config, item.inputs));
    const node = applyPresenceMotion(payload, item, resolved);
    if (!node.box) throw new Error(`Component collection item ${item.id} has no resolved box`);
    return {
      item,
      node,
      box: node.box,
      fixedGapBefore: item.gapBeforeMode === "fixed"
        ? Math.max(0, numberToken(payload, item.gapBeforeToken) * scale)
        : 0,
    };
  });
  if (itemSizingMode === "intrinsic" || intrinsic.length === 0) return intrinsic;
  const width = Math.max(...intrinsic.map(({ box }) => box.width));
  const height = Math.max(...intrinsic.map(({ box }) => box.height));
  const assignedBox = boundedCenterBox(payload, width, height);
  return items.map((item) => {
    const child = renderChild(
      embeddedComponentPayload(payload, item.componentType, item.config, item.inputs),
      assignedBox,
    );
    const resolved = applyPresenceMotion(payload, item, child);
    if (!resolved.box) throw new Error(`Component collection item ${item.id} has no resolved box`);
    const node = Math.abs(resolved.box.width - width) <= 0.001
        && Math.abs(resolved.box.height - height) <= 0.001
      ? resolved
      : translateRenderableNode(resolved, {
          x: assignedBox.x + (assignedBox.width - resolved.box.width) / 2 - resolved.box.x,
          y: assignedBox.y + (assignedBox.height - resolved.box.height) / 2 - resolved.box.y,
        }, assignedBox);
    return {
      item,
      node,
      box: assignedBox,
      fixedGapBefore: item.gapBeforeMode === "fixed"
        ? Math.max(0, numberToken(payload, item.gapBeforeToken) * scale)
        : 0,
    };
  });
}

function applyPresenceMotion(
  payload: DesignPreviewPayload,
  item: ComponentCollectionItemContract,
  node: RenderableNode,
) {
  if (!node.box) return node;
  const parentBox = previewScreenBox(payload);
  if (item.presenceTransition) {
    const frame = { trigger: true, elapsedMs: item.presenceElapsedMs ?? 0 };
    const wrapped = item.present
      ? wrapMotionFrame(payload, node, item.presenceMotion, frame, node.box, parentBox)
      : wrapExitMotionFrame(payload, node, item.presenceMotion, frame, node.box, parentBox);
    return { ...wrapped, box: node.box };
  }
  if (item.exitFrame !== undefined) {
    const elapsedFrames = Math.max(0, payload.localFrame - item.exitFrame);
    const wrapped = wrapExitMotionFrame(
      payload,
      node,
      item.presenceMotion,
      { trigger: true, elapsedMs: elapsedFrames / Math.max(1, payload.frameRate) * 1000 },
      node.box,
      parentBox,
    );
    return { ...wrapped, box: node.box };
  }
  if (item.activationFrame === undefined || item.activationFrame <= 0) return node;
  const elapsedFrames = Math.max(0, payload.localFrame - item.activationFrame);
  const wrapped = wrapMotionFrame(
    payload,
    node,
    item.presenceMotion,
    { trigger: true, elapsedMs: elapsedFrames / Math.max(1, payload.frameRate) * 1000 },
    node.box,
    parentBox,
  );
  return { ...wrapped, box: node.box };
}

function measureResolvedItems(
  payload: DesignPreviewPayload,
  items: ComponentCollectionLayoutItem[],
  resolveNode: (item: ComponentCollectionLayoutItem) => RenderableNode,
): MeasuredItem[] {
  const scale = renderScale(payload);
  return items.map((item) => {
    const node = resolveNode(item);
    if (!node.box) throw new Error(`Component collection item ${item.id} has no resolved box`);
    return {
      item,
      node,
      box: node.box,
      fixedGapBefore: item.gapBeforeMode === "fixed"
        ? Math.max(0, numberToken(payload, item.gapBeforeToken) * scale)
        : 0,
    };
  });
}

function alignedX(parent: RenderableBox, child: RenderableBox, alignment: ComponentCollectionItemContract["alignment"]) {
  if (alignment === "start") return parent.x;
  if (alignment === "end") return parent.x + parent.width - child.width;
  return parent.x + (parent.width - child.width) / 2;
}

function collectionGroup(id: string, box: RenderableBox, children: RenderableNode[]): RenderableNode {
  return {
    id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children,
  };
}
