import type { RenderableNode } from "../visual/renderable/types.js";
import {
  renderComponentCollectionFlow,
  renderComponentCollectionStacked,
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
  };
  return stack.distributionMode === "flow"
    ? renderComponentCollectionFlow(payload, stack.items, renderChild, base)
    : renderComponentCollectionStacked(payload, stack.items, renderChild, {
        ...base,
        direction: stack.stackDirection,
        offsetToken: stack.stackOffsetToken,
      });
}
