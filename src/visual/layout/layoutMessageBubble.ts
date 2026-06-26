import type { ResolvedMessageBubbleProps } from "../../domain/schemas/index.js";
import type { RenderableBox } from "../renderable/types.js";
import { measureTextApproximate } from "./textMeasurement.js";
import type { MessageBubbleLayout } from "./types.js";

export interface LayoutMessageBubbleInput {
  props: ResolvedMessageBubbleProps;
  messageArea: RenderableBox;
  y: number;
}

function readRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function readNumber(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

export function layoutMessageBubble({
  props,
  messageArea,
  y,
}: LayoutMessageBubbleInput): MessageBubbleLayout {
  const hasReceivedAvatar =
    props.direction === "incoming" && props.actor.avatarUri !== undefined;
  const avatarReserve = hasReceivedAvatar
    ? props.style.avatarSize + props.layout.avatarGap
    : 0;
  const availableBubbleWidth = Math.max(1, messageArea.width - avatarReserve);
  const maxBubbleWidth = Math.min(
    props.layout.maxWidth,
    availableBubbleWidth,
  );
  const maxTextWidth = Math.max(
    1,
    maxBubbleWidth - props.style.paddingX * 2,
  );
  const measurement = measureTextApproximate({
    text: props.visibleText,
    fontSize: props.style.fontSize,
    lineHeight: props.style.lineHeight,
    maxWidth: maxTextWidth,
  });
  const mediaWindow = readRecord(props.media?.window);
  const rawMediaWidth = props.media
    ? readNumber(mediaWindow.width, 0)
    : 0;
  const rawMediaHeight = props.media
    ? readNumber(mediaWindow.height, 0)
    : 0;
  const hasMedia = rawMediaWidth > 0 && rawMediaHeight > 0;
  const mediaWidth = hasMedia ? Math.min(maxTextWidth, rawMediaWidth) : 0;
  const mediaHeight =
    hasMedia && rawMediaWidth > 0
      ? Math.round((rawMediaHeight * mediaWidth) / rawMediaWidth)
      : 0;
  const mediaTextGap = hasMedia && props.visibleText.length > 0
    ? Math.max(2, Math.round(props.style.paddingY * 0.75))
    : 0;
  const contentWidth = Math.max(measurement.width, mediaWidth);
  const contentHeight = mediaHeight + mediaTextGap + measurement.height;
  const bubbleWidth = Math.min(
    maxBubbleWidth,
    Math.max(
      props.style.paddingX * 2 + props.style.fontSize * 0.52,
      contentWidth + props.style.paddingX * 2,
    ),
  );
  const bubbleHeight = contentHeight + props.style.paddingY * 2;
  const alignment = props.layout.alignment;
  const bubbleX =
    alignment === "right"
      ? messageArea.x + messageArea.width - bubbleWidth
      : alignment === "center"
        ? messageArea.x + (messageArea.width - bubbleWidth) / 2
        : messageArea.x + avatarReserve;
  const roundedBubbleWidth = Math.round(bubbleWidth);
  const roundedBubbleHeight = Math.round(bubbleHeight);
  const roundedBubbleX =
    alignment === "right"
      ? Math.round(messageArea.x + messageArea.width) - roundedBubbleWidth
      : alignment === "center"
        ? Math.round(messageArea.x + (messageArea.width - roundedBubbleWidth) / 2)
        : Math.round(bubbleX);
  const bubbleBox = {
    x: Math.max(
      Math.round(messageArea.x),
      Math.min(
        roundedBubbleX,
        Math.round(messageArea.x + messageArea.width) - roundedBubbleWidth,
      ),
    ),
    y: Math.round(y),
    width: roundedBubbleWidth,
    height: roundedBubbleHeight,
  };
  const textBox = {
    x: Math.round(bubbleBox.x + props.style.paddingX),
    y: Math.round(bubbleBox.y + props.style.paddingY + mediaHeight + mediaTextGap),
    width: Math.round(
      Math.max(0, bubbleBox.width - props.style.paddingX * 2),
    ),
    height: measurement.height,
  };
  const mediaBox = hasMedia
    ? {
        x: Math.round(bubbleBox.x + props.style.paddingX),
        y: Math.round(bubbleBox.y + props.style.paddingY),
        width: Math.round(mediaWidth),
        height: Math.round(mediaHeight),
      }
    : undefined;
  const avatarBox = hasReceivedAvatar
    ? {
        x: messageArea.x,
        y: Math.round(
          bubbleBox.y + bubbleBox.height - props.style.avatarSize,
        ),
        width: props.style.avatarSize,
        height: props.style.avatarSize,
      }
    : undefined;

  return {
    bubbleBox,
    ...(mediaBox ? { mediaBox } : {}),
    textBox,
    ...(avatarBox ? { avatarBox } : {}),
    measurement,
    maxBubbleWidth,
    alignment,
  };
}
