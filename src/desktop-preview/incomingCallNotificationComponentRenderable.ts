import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import {
  boundedCenterBox,
  numberToken,
  previewScreenBox,
  renderScale,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  iconRowComponentToRenderableAt,
  measureIconRowComponent,
} from "./iconRowComponentRenderable.js";
import type { IncomingCallNotificationDesignContract } from "./incomingCallNotificationComponentContract.js";
import { labelComponentToRenderableAt } from "./labelComponentRenderable.js";
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

  const content = notification.layout === "compact"
    ? compactChildren(payload, notification, inner, scale)
    : stackedChildren(payload, notification, inner, scale);
  const node: RenderableNode = {
    id: notification.id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children: [
      surfaceComponentToRenderableAt(payload, notification.surface, box),
      ...content,
    ],
  };

  if (notification.presenceTransition) {
    const frame = {
      trigger: true,
      elapsedMs: notification.presenceElapsedMs,
    };
    return notification.present
      ? wrapMotionFrame(
          payload,
          node,
          notification.boundaryMotion,
          frame,
          box,
          previewScreenBox(payload),
        )
      : wrapExitMotionFrame(
          payload,
          node,
          notification.boundaryMotion,
          frame,
          box,
          previewScreenBox(payload),
        );
  }
  return notification.present
    ? node
    : { ...node, transform: { ...node.transform, opacity: 0 } };
}

function compactChildren(
  payload: DesignPreviewPayload,
  notification: IncomingCallNotificationDesignContract,
  inner: RenderableBox,
  scale: number,
) {
  const gap = Math.max(0, numberToken(payload, notification.contentGapToken) * scale);
  const avatarSize = notification.avatarSize * scale;
  const actionSize = measureIconRowComponent(payload, notification.iconRow);
  const labelWidth = inner.width - avatarSize - actionSize.width - gap * 2;
  if (labelWidth <= 0 || Math.max(avatarSize, actionSize.height) > inner.height) {
    throw new Error(
      "component.incomingCallNotification compact children do not fit its Variant size",
    );
  }
  const avatarBox = verticallyCentered(inner, avatarSize, avatarSize, inner.x);
  const labelBox = {
    x: avatarBox.x + avatarBox.width + gap,
    y: inner.y,
    width: labelWidth,
    height: inner.height,
  };
  const actionsBox = verticallyCentered(
    inner,
    actionSize.width,
    actionSize.height,
    inner.x + inner.width - actionSize.width,
  );
  return [
    avatarComponentToRenderableAt(payload, notification.avatar, avatarBox),
    labelComponentToRenderableAt(
      payload,
      notification.label,
      labelBox,
      { maximumWidth: labelBox.width },
    ),
    iconRowComponentToRenderableAt(payload, notification.iconRow, actionsBox),
  ];
}

function stackedChildren(
  payload: DesignPreviewPayload,
  notification: IncomingCallNotificationDesignContract,
  inner: RenderableBox,
  scale: number,
) {
  const contentGap = Math.max(
    0,
    numberToken(payload, notification.contentGapToken) * scale,
  );
  const sectionGap = Math.max(
    0,
    numberToken(payload, notification.sectionGapToken) * scale,
  );
  const avatarSize = notification.avatarSize * scale;
  const actionSize = measureIconRowComponent(payload, notification.iconRow);
  const identityHeight = inner.height - actionSize.height - sectionGap;
  const labelWidth = inner.width - avatarSize - contentGap;
  if (identityHeight <= 0
      || labelWidth <= 0
      || avatarSize > identityHeight
      || actionSize.width > inner.width) {
    throw new Error(
      "component.incomingCallNotification stacked children do not fit its Variant size",
    );
  }
  const identityBox = {
    x: inner.x,
    y: inner.y,
    width: inner.width,
    height: identityHeight,
  };
  const avatarBox = verticallyCentered(
    identityBox,
    avatarSize,
    avatarSize,
    identityBox.x,
  );
  const labelBox = {
    x: avatarBox.x + avatarBox.width + contentGap,
    y: identityBox.y,
    width: labelWidth,
    height: identityBox.height,
  };
  const actionsBox = {
    x: inner.x + inner.width - actionSize.width,
    y: inner.y + inner.height - actionSize.height,
    width: actionSize.width,
    height: actionSize.height,
  };
  return [
    avatarComponentToRenderableAt(payload, notification.avatar, avatarBox),
    labelComponentToRenderableAt(
      payload,
      notification.label,
      labelBox,
      { maximumWidth: labelBox.width },
    ),
    iconRowComponentToRenderableAt(payload, notification.iconRow, actionsBox),
  ];
}

function verticallyCentered(
  bounds: RenderableBox,
  width: number,
  height: number,
  x: number,
) {
  return {
    x,
    y: bounds.y + (bounds.height - height) / 2,
    width,
    height,
  };
}
