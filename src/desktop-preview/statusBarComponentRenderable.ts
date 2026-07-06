import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { StatusBarDesignContract } from "./statusBarComponentContract.js";
import { statusBarToRenderable } from "./systemBarRenderables.js";

export function statusBarComponentToRenderable(
  payload: DesignPreviewPayload,
  statusBar: StatusBarDesignContract,
): RenderableNode {
  return statusBarToRenderable(payload, statusBar);
}
