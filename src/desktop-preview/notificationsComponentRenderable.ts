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
  if (notifications.distributionTransition) {
    return renderDistributionTransition(payload, notifications, current, renderChild);
  }
  const node = notifications.stack.distributionMode === "stacked"
    ? withBadge(payload, current, notifications)
    : current;
  return { ...node, id: notifications.id };
}

function withBadge(
  payload: DesignPreviewPayload,
  node: RenderableNode,
  notifications: NotificationsDesignContract,
) {
  if (!notifications.badge) return node;
  if (!node.box) throw new Error("Notifications Collection Stack must resolve a box before Badge placement");
  return {
    ...node,
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
  const previousStack = collectionStackComponentToRenderable(payload, transition.fromStack, renderChild);
  const previous = transition.fromMode === "stacked"
    ? withBadge(payload, previousStack, notifications)
    : previousStack;
  const decoratedCurrent = notifications.stack.distributionMode === "stacked"
    ? withBadge(payload, current, notifications)
    : current;
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
    decoratedCurrent,
    notifications.distributionMotion,
    { trigger: true, elapsedMs },
    decoratedCurrent.box!,
    parent,
  );
  return {
    id: `${notifications.id}.distribution-transition`,
    type: "group" as const,
    frame: 0,
    box: unionBoxes([previous.box, decoratedCurrent.box!]),
    style: { overflow: "visible" },
    children: [outgoing, incoming],
  };
}
