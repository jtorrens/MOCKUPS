import type { RenderableNode } from "../visual/renderable/types.js";
import { collectionStackComponentToRenderable } from "./collectionStackComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { NotificationsChildRenderer, NotificationsDesignContract } from "./notificationsComponentContract.js";
import { badgeComponentToRenderableAt } from "./badgeComponentRenderable.js";
import { previewScreenBox, unionBoxes } from "./componentRenderableCommon.js";
import { wrapExitMotionFrame, wrapMotionFrame } from "./previewMotionHelpers.js";

export function notificationsComponentToRenderable(
  payload: DesignPreviewPayload,
  notifications: NotificationsDesignContract,
  renderChild: NotificationsChildRenderer,
): RenderableNode {
  const current = collectionStackComponentToRenderable(payload, notifications.stack, renderChild);
  const node = notifications.distributionTransition
    ? renderDistributionTransition(payload, notifications, current, renderChild)
    : current;
  if (!notifications.badge) return { ...node, id: notifications.id };
  if (!node.box) throw new Error("Notifications Collection Stack must resolve a box before Badge placement");
  return {
    ...node,
    id: notifications.id,
    style: { ...node.style, overflow: "visible" },
    children: [...(node.children ?? []), badgeComponentToRenderableAt(payload, notifications.badge, node.box)],
  };
}

function renderDistributionTransition(
  payload: DesignPreviewPayload,
  notifications: NotificationsDesignContract,
  current: RenderableNode,
  renderChild: NotificationsChildRenderer,
) {
  const transition = notifications.distributionTransition!;
  const previous = collectionStackComponentToRenderable(payload, transition.fromStack, renderChild);
  if (!current.box || !previous.box) throw new Error("Notifications distribution transition requires resolved boxes");
  const elapsedMs = transition.elapsedFrames / Math.max(1, payload.frameRate) * 1000;
  const parent = previewScreenBox(payload);
  const outgoing = wrapExitMotionFrame(
    payload,
    previous,
    notifications.distributionMotion,
    { trigger: true, elapsedMs },
    previous.box,
    parent,
  );
  const incoming = wrapMotionFrame(
    payload,
    current,
    notifications.distributionMotion,
    { trigger: true, elapsedMs },
    current.box,
    parent,
  );
  return {
    id: `${notifications.id}.distribution-transition`,
    type: "group" as const,
    frame: 0,
    box: unionBoxes([previous.box, current.box]),
    style: { overflow: "visible" },
    children: [outgoing, incoming],
  };
}
