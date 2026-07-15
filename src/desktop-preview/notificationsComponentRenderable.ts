import type { RenderableNode } from "../visual/renderable/types.js";
import { collectionStackComponentToRenderable } from "./collectionStackComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { NotificationsChildRenderer, NotificationsDesignContract } from "./notificationsComponentContract.js";

export function notificationsComponentToRenderable(
  payload: DesignPreviewPayload,
  notifications: NotificationsDesignContract,
  renderChild: NotificationsChildRenderer,
): RenderableNode {
  const node = collectionStackComponentToRenderable(payload, notifications.stack, renderChild);
  return { ...node, id: notifications.id };
}
