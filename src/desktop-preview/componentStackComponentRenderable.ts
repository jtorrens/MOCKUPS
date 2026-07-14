import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type {
  ComponentStackChildRenderer,
  ComponentStackDesignContract,
  ComponentStackItemContract,
} from "./componentStackComponentContract.js";
import { boundedCenterBox, embeddedComponentPayload, numberToken, previewScreenBox, renderScale, translateRenderableNode } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

interface MeasuredItem {
  item: ComponentStackItemContract;
  node: RenderableNode;
  box: RenderableBox;
  fixedGapBefore: number;
}

export function componentStackComponentToRenderable(
  payload: DesignPreviewPayload,
  stack: ComponentStackDesignContract,
  renderChild: ComponentStackChildRenderer,
): RenderableNode {
  const scale = renderScale(payload);
  const startGap = Math.max(0, numberToken(payload, stack.startGapToken) * scale);
  const endGap = Math.max(0, numberToken(payload, stack.endGapToken) * scale);
  const measured: MeasuredItem[] = stack.items.map((item) => {
    const node = renderChild(embeddedComponentPayload(payload, item.componentType, item.config, item.inputs));
    if (!node.box) throw new Error(`Component stack item ${item.id} has no resolved box`);
    return {
      item,
      node,
      box: node.box,
      fixedGapBefore: item.gapBeforeMode === "fixed"
        ? Math.max(0, numberToken(payload, item.gapBeforeToken) * scale)
        : 0,
    };
  });
  const naturalWidth = measured.length ? Math.max(...measured.map(({ box }) => box.width)) : 0;
  const naturalHeight = startGap + endGap + measured.reduce((sum, current, index) =>
    sum + current.box.height + (index > 0 ? current.fixedGapBefore : 0), 0);
  const stackBox = stack.sizingMode === "fill"
    ? previewScreenBox(payload)
    : boundedCenterBox(payload, naturalWidth, naturalHeight);
  const fixedHeight = startGap + endGap + measured.reduce((sum, current, index) =>
    sum + current.box.height + (index > 0 ? current.fixedGapBefore : 0), 0);
  const totalWeight = measured.slice(1).reduce((sum, current) =>
    sum + (current.item.gapBeforeMode === "reflow" ? current.item.gapBeforeWeight : 0), 0);
  const reflowSpace = stack.sizingMode === "fill" ? Math.max(0, stackBox.height - fixedHeight) : 0;
  let cursorY = stackBox.y + startGap;
  const children = measured.map((current, index) => {
    if (index > 0) {
      cursorY += current.item.gapBeforeMode === "fixed"
        ? current.fixedGapBefore
        : totalWeight > 0 ? reflowSpace * current.item.gapBeforeWeight / totalWeight : 0;
    }
    const x = alignedX(stackBox, current.box, current.item.alignment);
    const translated = translateRenderableNode(current.node, {
      x: x - current.box.x,
      y: cursorY - current.box.y,
    });
    cursorY += current.box.height;
    return translated;
  });
  return {
    id: stack.id,
    type: "group",
    frame: 0,
    box: stackBox,
    style: { overflow: "visible" },
    children,
  };
}

function alignedX(parent: RenderableBox, child: RenderableBox, alignment: ComponentStackItemContract["alignment"]) {
  if (alignment === "start") return parent.x;
  if (alignment === "end") return parent.x + parent.width - child.width;
  return parent.x + (parent.width - child.width) / 2;
}
