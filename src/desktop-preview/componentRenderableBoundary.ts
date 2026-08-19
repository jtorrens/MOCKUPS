import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  routeComponentClassToRenderable,
} from "./componentClassRenderableRegistry.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { withAuthoringTarget } from "./previewAuthoringTarget.js";
import { resolveRenderablePayload } from "./renderablePayloadBoundary.js";

export interface ResolvedComponentRenderable<TResolved> {
  renderable: RenderableNode;
  resolved: TResolved;
}

export function resolveComponentRenderable<TResolved>(
  payload: DesignPreviewPayload,
  resolver: (resolvedPayload: DesignPreviewPayload) => TResolved,
  renderable: (
    resolvedPayload: DesignPreviewPayload,
    resolved: TResolved,
  ) => RenderableNode,
): ResolvedComponentRenderable<TResolved> {
  const resolvedPayload = resolveRenderablePayload(payload);
  const resolved = resolver(resolvedPayload);
  return {
    renderable: withAuthoringTarget(
      payload,
      renderable(resolvedPayload, resolved),
    ),
    resolved,
  };
}

export function componentClassToRenderable(
  payload: DesignPreviewPayload,
  assignedBox?: RenderableBox,
): RenderableNode {
  return withAuthoringTarget(
    payload,
    routeComponentClassToRenderable(
      resolveRenderablePayload(payload),
      componentClassToRenderable,
      assignedBox,
    ),
  );
}
