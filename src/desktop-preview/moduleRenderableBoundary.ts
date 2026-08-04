import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { routeModuleToRenderable } from "./moduleRenderableRegistry.js";
import { validateRootFrameIdentity } from "./previewFrameContext.js";
import { resolveRenderablePayload } from "./renderablePayloadBoundary.js";
import { applyDeviceModuleTransparency } from "./deviceModuleTransparency.js";

export function moduleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const resolved = resolveRenderablePayload(payload);
  validateRootFrameIdentity(resolved);
  return applyDeviceModuleTransparency(
    resolved,
    routeModuleToRenderable(resolved),
  );
}
