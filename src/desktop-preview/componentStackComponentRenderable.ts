import type { RenderableNode } from "../visual/renderable/types.js";
import type {
  ComponentStackChildRenderer,
  ComponentStackDesignContract,
} from "./componentStackComponentContract.js";
import { renderComponentCollectionFlow } from "./componentCollectionRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export function componentStackComponentToRenderable(
  payload: DesignPreviewPayload,
  stack: ComponentStackDesignContract,
  renderChild: ComponentStackChildRenderer,
): RenderableNode {
  return renderComponentCollectionFlow(payload, stack.items, renderChild, {
    id: stack.id,
    sizingMode: stack.sizingMode,
    startGapToken: stack.startGapToken,
    endGapToken: stack.endGapToken,
  });
}
