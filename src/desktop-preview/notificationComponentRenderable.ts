import type { RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { boundedCenterBox, numberToken, placeChild, renderScale, scalePlacement } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { labelComponentToRenderableAt, measureLabelComponent } from "./labelComponentRenderable.js";
import type { NotificationDesignContract } from "./notificationComponentContract.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function notificationComponentToRenderable(
  payload: DesignPreviewPayload,
  notification: NotificationDesignContract,
  assignedBox?: { x: number; y: number; width: number; height: number },
): RenderableNode {
  const scale = renderScale(payload);
  const avatarSize = notification.avatar.size * scale;
  const gap = Math.max(0, numberToken(payload, notification.gapToken) * scale);
  const paddingX = numberToken(payload, notification.padding.xToken) * scale;
  const paddingY = numberToken(payload, notification.padding.yToken) * scale;
  const maximumWidth = notification.maxWidth * scale;
  const maximumLabelWidth = Math.max(1, maximumWidth - paddingX * 2 - avatarSize - gap);
  const labelLayout = notification.dimensionMode === "content"
    ? { maximumWidth: maximumLabelWidth }
    : {};
  const labelSize = measureLabelComponent(notification.label, payload, labelLayout);
  const fromLabelSize = notification.reflow
    ? measureLabelComponent(notification.reflow.fromLabel, payload, labelLayout)
    : labelSize;
  const contentWidth = avatarSize + gap + labelSize.width;
  const contentHeight = Math.max(avatarSize, labelSize.height);
  const fromContentWidth = avatarSize + gap + fromLabelSize.width;
  const fromContentHeight = Math.max(avatarSize, fromLabelSize.height);
  const targetWidth = notification.dimensionMode === "fixed" ? notification.size.width * scale : contentWidth + paddingX * 2;
  const targetHeight = notification.dimensionMode === "fixed" ? notification.size.height * scale : contentHeight + paddingY * 2;
  const fromWidth = notification.dimensionMode === "fixed" ? targetWidth : fromContentWidth + paddingX * 2;
  const fromHeight = notification.dimensionMode === "fixed" ? targetHeight : fromContentHeight + paddingY * 2;
  const progress = notification.reflow?.progress ?? 1;
  const width = lerp(fromWidth, targetWidth, progress);
  const height = lerp(fromHeight, targetHeight, progress);
  const box = assignedBox ?? boundedCenterBox(payload, width, height);
  const innerBox = {
    x: box.x + paddingX,
    y: box.y + paddingY,
    width: Math.max(0, box.width - paddingX * 2),
    height: Math.max(0, box.height - paddingY * 2),
  };
  let avatarBox = placeChild(innerBox, { width: avatarSize, height: avatarSize }, scalePlacement(notification.avatarPlacement, scale));
  let labelBox = placeChild(innerBox, labelSize, scalePlacement(notification.labelPlacement, scale));
  const avatarIsFirst = avatarBox.x + avatarBox.width / 2 <= labelBox.x + labelBox.width / 2;
  const separated = avatarIsFirst
    ? enforceHorizontalGap(
        avatarBox,
        labelBox,
        notification.avatarPlacement.mode,
        notification.labelPlacement.mode,
        gap,
        innerBox,
      )
    : enforceHorizontalGap(
        labelBox,
        avatarBox,
        notification.labelPlacement.mode,
        notification.avatarPlacement.mode,
        gap,
        innerBox,
      );
  if (avatarIsFirst) {
    [avatarBox, labelBox] = separated;
  } else {
    [labelBox, avatarBox] = separated;
  }
  const avatar = avatarComponentToRenderableAt(payload, notification.avatar, avatarBox);
  const label = labelComponentToRenderableAt(payload, notification.label, labelBox, labelLayout);
  const surface = surfaceComponentToRenderableAt(payload, notification.surface, box);
  const labels = notification.reflow
    ? [
        {
          ...labelComponentToRenderableAt(
            payload,
            notification.reflow.fromLabel,
            placeChild(innerBox, fromLabelSize, scalePlacement(notification.labelPlacement, scale)),
            labelLayout,
          ),
          transform: { opacity: 1 - progress },
        },
        { ...label, transform: { opacity: progress } },
      ]
    : [label];
  return {
    id: notification.id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children: [surface, avatar, ...labels],
  };
}

function lerp(start: number, end: number, amount: number) {
  return start + (end - start) * Math.max(0, Math.min(1, amount));
}

function enforceHorizontalGap(
  first: { x: number; y: number; width: number; height: number },
  second: { x: number; y: number; width: number; height: number },
  firstMode: NotificationDesignContract["avatarPlacement"]["mode"],
  secondMode: NotificationDesignContract["avatarPlacement"]["mode"],
  gap: number,
  bounds: { x: number; width: number },
) {
  let remaining = gap - (second.x - (first.x + first.width));
  if (remaining <= 0) return [first, second] as const;

  const maxFirstShift = firstMode === "insideEdge"
    ? Math.max(0, first.x - bounds.x)
    : Number.POSITIVE_INFINITY;
  const boundsRight = bounds.x + bounds.width;
  const maxSecondShift = secondMode === "insideEdge"
    ? Math.max(0, boundsRight - (second.x + second.width))
    : Number.POSITIVE_INFINITY;
  let firstShift = Math.min(remaining / 2, maxFirstShift);
  let secondShift = Math.min(remaining - firstShift, maxSecondShift);
  remaining -= firstShift + secondShift;
  if (remaining > 0) {
    const extraFirst = Math.min(remaining, maxFirstShift - firstShift);
    firstShift += extraFirst;
    remaining -= extraFirst;
  }
  if (remaining > 0) {
    const extraSecond = Math.min(remaining, maxSecondShift - secondShift);
    secondShift += extraSecond;
  }
  return [
    { ...first, x: first.x - firstShift },
    { ...second, x: second.x + secondShift },
  ] as const;
}
