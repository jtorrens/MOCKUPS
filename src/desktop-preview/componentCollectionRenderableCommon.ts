import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type {
  ComponentCollectionChildRenderer,
  ComponentCollectionItemContract,
  ComponentCollectionSizingMode,
} from "./componentCollectionContract.js";
import { boundedCenterBox, embeddedComponentPayload, numberToken, previewScreenBox, renderScale, translateRenderableNode } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

interface MeasuredItem {
  item: ComponentCollectionItemContract;
  node: RenderableNode;
  box: RenderableBox;
  fixedGapBefore: number;
}

interface FlowOptions {
  id: string;
  sizingMode: ComponentCollectionSizingMode;
  startGapToken: string;
  endGapToken: string;
}

interface StackedOptions extends FlowOptions {
  direction: "down" | "up";
  offsetToken: string;
}

export function renderComponentCollectionFlow(
  payload: DesignPreviewPayload,
  items: ComponentCollectionItemContract[],
  renderChild: ComponentCollectionChildRenderer,
  options: FlowOptions,
): RenderableNode {
  const scale = renderScale(payload);
  const startGap = Math.max(0, numberToken(payload, options.startGapToken) * scale);
  const endGap = Math.max(0, numberToken(payload, options.endGapToken) * scale);
  const measured = measureItems(payload, items, renderChild);
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
  const measured = measureItems(payload, items, renderChild);
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
    return translateRenderableNode(current.node, {
      x: x - current.box.x,
      y: y - current.box.y,
    });
  });
  return collectionGroup(options.id, parentBox, children);
}

function measureItems(
  payload: DesignPreviewPayload,
  items: ComponentCollectionItemContract[],
  renderChild: ComponentCollectionChildRenderer,
): MeasuredItem[] {
  const scale = renderScale(payload);
  return items.map((item) => {
    const node = renderChild(embeddedComponentPayload(payload, item.componentType, item.config, item.inputs));
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
