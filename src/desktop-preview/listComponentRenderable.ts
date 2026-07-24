import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { collectionStackComponentToRenderable } from "./collectionStackComponentRenderable.js";
import { translateRenderableNode } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import type {
  ListChildRenderer,
  ListDesignContract,
} from "./listComponentContract.js";

export function listComponentToRenderable(
  payload: DesignPreviewPayload,
  list: ListDesignContract,
  assignedBox?: RenderableBox,
  renderChild?: ListChildRenderer,
): RenderableNode {
  if (!renderChild) {
    throw new Error("component.list requires its declared child renderer");
  }
  const stack = collectionStackComponentToRenderable(payload, list.stack, renderChild);
  if (!stack.box) {
    throw new Error("component.list Collection Stack must resolve a box");
  }
  const viewport = assignedBox ?? stack.box;
  const targetY = list.itemsPlacement === "top"
    ? viewport.y
    : list.itemsPlacement === "bottom"
      ? viewport.y + viewport.height - stack.box.height
      : viewport.y + (viewport.height - stack.box.height) / 2;
  const translatedStack = translateRenderableNode(stack, {
    x: viewport.x + (viewport.width - stack.box.width) / 2 - stack.box.x,
    y: targetY - stack.box.y,
  });
  const surface = surfaceComponentToRenderableAt(
    payload,
    list.surface,
    viewport,
  );
  return {
    id: list.id,
    type: "group",
    frame: 0,
    box: viewport,
    style: { overflow: list.overflowMode === "clip" ? "hidden" : "visible" },
    children: [surface, translatedStack],
  };
}
