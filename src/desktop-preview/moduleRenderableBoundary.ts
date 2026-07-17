import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { routeModuleToRenderable } from "./moduleRenderableRegistry.js";
import { resolveRenderablePayload } from "./renderablePayloadBoundary.js";

export function moduleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  return routeModuleToRenderable(resolveRenderablePayload(payload));
}
