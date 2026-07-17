import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  routeComponentClassToRenderable,
} from "./componentClassRenderableRegistry.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveRenderablePayload } from "./renderablePayloadBoundary.js";

export function componentClassToRenderable(
  payload: DesignPreviewPayload,
  assignedBox?: RenderableBox,
): RenderableNode {
  return routeComponentClassToRenderable(
    resolveRenderablePayload(payload),
    componentClassToRenderable,
    assignedBox,
  );
}
