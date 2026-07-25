import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import {
  boundedCenterBox,
  numberToken,
  placeChild,
  previewScreenBox,
  renderScale,
  scalePlacement,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  iconRowAssignedSize,
  iconRowComponentToRenderableAt,
  measureIconRowComponent,
} from "./iconRowComponentRenderable.js";
import type { IncomingCallNotificationDesignContract } from "./incomingCallNotificationComponentContract.js";
import {
  wrapExitMotionFrame,
  wrapMotionFrame,
} from "./previewMotionHelpers.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function incomingCallNotificationComponentToRenderable(
  payload: DesignPreviewPayload,
  notification: IncomingCallNotificationDesignContract,
  assignedBox?: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const box = assignedBox ?? boundedCenterBox(
    payload,
    notification.size.width * scale,
    notification.size.height * scale,
  );
  const paddingX = Math.max(0, numberToken(payload, notification.padding.xToken) * scale);
  const paddingY = Math.max(0, numberToken(payload, notification.padding.yToken) * scale);
  const inner: RenderableBox = {
    x: box.x + paddingX,
    y: box.y + paddingY,
    width: box.width - paddingX * 2,
    height: box.height - paddingY * 2,
  };
  if (inner.width <= 0 || inner.height <= 0) {
    throw new Error(
      "component.incomingCallNotification size must exceed its Variant padding",
    );
  }

  const avatarSize = notification.avatar.size * scale;
  const avatarBox = placeChild(
    inner,
    { width: avatarSize, height: avatarSize },
    scalePlacement(notification.avatarPlacement, scale),
  );
  const iconRowSize = iconRowAssignedSize(
    notification.iconRow,
    measureIconRowComponent(payload, notification.iconRow),
    inner,
  );
  const iconRowBox = placeChild(
    inner,
    iconRowSize,
    scalePlacement(notification.iconRowPlacement, scale),
  );
  const node: RenderableNode = {
    id: notification.id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children: [
      surfaceComponentToRenderableAt(payload, notification.surface, box),
      avatarComponentToRenderableAt(payload, notification.avatar, avatarBox),
      iconRowComponentToRenderableAt(payload, notification.iconRow, iconRowBox),
    ],
  };

  if (notification.boundaryMotionFrame.active) {
    return notification.present
      ? wrapMotionFrame(
          payload,
          node,
          notification.boundaryMotion,
          notification.boundaryMotionFrame,
          box,
          previewScreenBox(payload),
        )
      : wrapExitMotionFrame(
          payload,
          node,
          notification.boundaryMotion,
          notification.boundaryMotionFrame,
          box,
          previewScreenBox(payload),
        );
  }
  return notification.present
    ? node
    : { ...node, transform: { ...node.transform, opacity: 0 } };
}
