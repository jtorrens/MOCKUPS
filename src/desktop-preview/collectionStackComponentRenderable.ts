import type { RenderableNode } from "../visual/renderable/types.js";
import {
  renderComponentCollectionFlow,
  renderComponentCollectionStacked,
  interpolateComponentCollectionReflow,
} from "./componentCollectionRenderableCommon.js";
import type {
  CollectionStackChildRenderer,
  CollectionStackDesignContract,
} from "./collectionStackComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function collectionStackComponentToRenderable(
  payload: DesignPreviewPayload,
  stack: CollectionStackDesignContract,
  renderChild: CollectionStackChildRenderer,
): RenderableNode {
  const base = {
    id: stack.id,
    sizingMode: stack.sizingMode,
    startGapToken: stack.startGapToken,
    endGapToken: stack.endGapToken,
    itemSizingMode: stack.itemSizingMode,
  };
  if (stack.distributionMode === "flow") {
    const current = renderComponentCollectionFlow(payload, stack.items, renderChild, base);
    if (!stack.reflow) return current;
    const previous = renderComponentCollectionFlow(payload, stack.reflow.fromItems, renderChild, base);
    return interpolateComponentCollectionReflow(
      previous,
      stack.reflow.fromItems,
      current,
      stack.items,
      stack.reflow.progress,
    );
  }
  return renderComponentCollectionStacked(payload, stack.items, renderChild, {
        ...base,
        direction: stack.stackDirection,
        offsetToken: stack.stackOffsetToken,
        scaleRatio: stack.scaleRatio,
        opacityRatio: stack.opacityRatio,
      });
}
