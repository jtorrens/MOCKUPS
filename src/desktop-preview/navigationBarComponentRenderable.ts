import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { NavigationBarDesignContract } from "./navigationBarComponentContract.js";
import { navigationBarToRenderable } from "./systemBarRenderables.js";

export function navigationBarComponentToRenderable(
  payload: DesignPreviewPayload,
  navigationBar: NavigationBarDesignContract,
): RenderableNode {
  return navigationBarToRenderable(payload, navigationBar);
}
