import type { RenderableNode } from "../visual/renderable/types.js";
import { collectionStackComponentToRenderable } from "./collectionStackComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type {
  ListChildRenderer,
  ListDesignContract,
} from "./listComponentContract.js";

export function listComponentToRenderable(
  payload: DesignPreviewPayload,
  list: ListDesignContract,
  renderChild: ListChildRenderer,
): RenderableNode {
  return {
    ...collectionStackComponentToRenderable(payload, list.stack, renderChild),
    id: list.id,
  };
}
