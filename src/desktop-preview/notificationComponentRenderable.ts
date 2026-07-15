import type { RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { boundedCenterBox, numberToken, renderScale, unionBoxes } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { labelComponentToRenderableAt, measureLabelComponent } from "./labelComponentRenderable.js";
import type { NotificationDesignContract } from "./notificationComponentContract.js";

export function notificationComponentToRenderable(
  payload: DesignPreviewPayload,
  notification: NotificationDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const avatarSize = notification.avatar.size * scale;
  const labelSize = measureLabelComponent(notification.label, payload);
  const gap = Math.max(0, numberToken(payload, notification.gapToken) * scale);
  const width = avatarSize + gap + labelSize.width;
  const height = Math.max(avatarSize, labelSize.height);
  const box = boundedCenterBox(payload, width, height);
  const avatarBox = {
    x: notification.avatarPosition === "start" ? box.x : box.x + labelSize.width + gap,
    y: box.y + (box.height - avatarSize) / 2,
    width: avatarSize,
    height: avatarSize,
  };
  const labelBox = {
    x: notification.avatarPosition === "start" ? box.x + avatarSize + gap : box.x,
    y: box.y + (box.height - labelSize.height) / 2,
    width: labelSize.width,
    height: labelSize.height,
  };
  const avatar = avatarComponentToRenderableAt(payload, notification.avatar, avatarBox);
  const label = labelComponentToRenderableAt(payload, notification.label, labelBox);
  return {
    id: notification.id,
    type: "group",
    frame: 0,
    box: unionBoxes([avatar.box ?? avatarBox, label.box ?? labelBox]),
    style: { overflow: "visible" },
    children: notification.avatarPosition === "start" ? [avatar, label] : [label, avatar],
  };
}
