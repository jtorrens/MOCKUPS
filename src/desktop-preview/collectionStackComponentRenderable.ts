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
  const renderDistribution = (
    distributionMode: CollectionStackDesignContract["distributionMode"],
    items: CollectionStackDesignContract["items"],
  ) => distributionMode === "flow"
    ? renderComponentCollectionFlow(payload, items, renderChild, base)
    : renderComponentCollectionStacked(payload, items, renderChild, {
        ...base,
        direction: stack.stackDirection,
        offsetToken: stack.stackOffsetToken,
        scaleRatio: stack.scaleRatio,
        opacityRatio: stack.opacityRatio,
      });
  const current = renderDistribution(stack.distributionMode, stack.items);
  if (stack.reflow) {
    const previous = renderDistribution(
      stack.reflow.fromDistributionMode ?? stack.distributionMode,
      stack.reflow.fromItems,
    );
    return interpolateComponentCollectionReflow(
      previous,
      stack.reflow.fromItems,
      current,
      stack.items,
      stack.reflow.progress,
    );
  }
  return current;
}
