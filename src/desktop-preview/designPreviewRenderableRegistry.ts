import type { RenderableNode } from "../visual/renderable/types.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { moduleToRenderable } from "./moduleRenderableBoundary.js";

type DesignPreviewKind = DesignPreviewPayload["kind"];
type DesignPreviewRenderableFactory = (payload: DesignPreviewPayload) => RenderableNode;

const designPreviewRenderableFactories = {
  componentClass: componentClassToRenderable,
  module: moduleToRenderable,
  moduleInstance: moduleToRenderable,
} satisfies Record<DesignPreviewKind, DesignPreviewRenderableFactory>;

export function designPreviewPayloadToRenderable(
  payload: DesignPreviewPayload,
): RenderableNode {
  const factory = designPreviewRenderableFactories[payload.kind];
  if (!factory) {
    throw new Error(`Unsupported design preview route '${String(payload.kind)}'.`);
  }
  return factory(payload);
}
