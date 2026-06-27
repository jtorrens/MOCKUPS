import type { ResolvedMessageBubbleProps } from "../../domain/schemas/index.js";
import type { RenderableBox } from "../renderable/types.js";
import { measureTextApproximate } from "./textMeasurement.js";
import type { MessageBubbleLayout, TextMeasurer } from "./types.js";

export interface LayoutMessageBubbleInput {
  props: ResolvedMessageBubbleProps;
  messageArea: RenderableBox;
  measurer?: TextMeasurer;
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

function readString(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function tailProtrusion(props: ResolvedMessageBubbleProps) {
  if (!props.layout.showTail || props.direction === "system") {
    return { left: 0, right: 0 };
  }
  if (props.style.tailStyle === "none") {
    return { left: 0, right: 0 };
  }
  const width = Math.round(
    props.style.tailWidth * Math.max(0.01, props.style.tailScale),
  );
  if (width <= 0) return { left: 0, right: 0 };
  return props.direction === "outgoing"
    ? { left: 0, right: width - Math.ceil(width * 0.34) }
    : { left: Math.floor(width * 0.66), right: 0 };
}

function measureStatus(props: ResolvedMessageBubbleProps) {
  const status = readRecord(props.status);
  const statusStyle = readRecord(props.style.status);
  const deliveryStatus = readString(status.deliveryStatus, "none");
  const text = readString(status.text);
  const showText = statusStyle.showText !== false && text.trim().length > 0;
  const showTicks =
    props.direction === "outgoing" &&
    statusStyle.showTicks !== false &&
    deliveryStatus !== "none";
  if (!showText && !showTicks) {
    return {
      visible: false,
      width: 0,
      height: 0,
      offsetX: 0,
      offsetY: 0,
    };
  }
  const size = readNumber(statusStyle.size, 11);
  const gap = readNumber(statusStyle.gap, 3);
  const lineHeight = Math.round(size * 1.15);
  const textWidth = showText
    ? Math.ceil(Array.from(text).length * size * 0.54)
    : 0;
  const isDoubleTick = deliveryStatus === "delivered" || deliveryStatus === "read";
  const tickWidth = showTicks ? Math.ceil(size * (isDoubleTick ? 1.45 : 1)) : 0;
  return {
    visible: true,
    width: Math.max(
      1,
      textWidth + tickWidth + (showText && showTicks ? Math.max(0, gap) : 0),
    ),
    height: lineHeight,
    offsetX: readNumber(statusStyle.offsetX, -8),
    offsetY: readNumber(statusStyle.offsetY, -5),
  };
}

function measureMessageLabel(
  props: ResolvedMessageBubbleProps,
  measurer: TextMeasurer | undefined,
) {
  const labelStyle = readRecord(props.style.label);
  if (labelStyle.visible !== true || props.direction !== "incoming") {
    return {
      visible: false,
      width: 0,
      height: 0,
      offsetX: 0,
      offsetY: 0,
      reserveHeight: 0,
    };
  }
  const text = props.actor.displayName;
  const fontSize = readNumber(labelStyle.fontSize, Math.max(1, props.style.fontSize * 0.78));
  const lineHeight = readNumber(labelStyle.lineHeight, Math.round(fontSize * 1.2));
  const paddingX = readNumber(labelStyle.paddingX, 8);
  const paddingY = readNumber(labelStyle.paddingY, 4);
  const textWidth =
    measurer?.measureLineWidth(text, {
      fontFamily: props.style.fontFamily,
      fontSize,
      fontWeight: readString(labelStyle.fontWeight, "Regular"),
    }) ?? Math.ceil(Array.from(text).length * fontSize * 0.56);
  const sizingMode = readString(labelStyle.sizingMode, "content");
  const width =
    sizingMode === "fixed"
      ? Math.max(1, readNumber(labelStyle.width, textWidth + paddingX * 2))
      : Math.max(1, textWidth + paddingX * 2);
  const height =
    sizingMode === "fixed"
      ? Math.max(1, readNumber(labelStyle.height, lineHeight + paddingY * 2))
      : Math.max(1, lineHeight + paddingY * 2);
  const offsetY = readNumber(labelStyle.offsetY, 0);
  return {
    visible: true,
    width,
    height,
    offsetX: readNumber(labelStyle.offsetX, 0),
    offsetY,
    reserveHeight: Math.max(0, height + offsetY + props.style.paddingY),
  };
}

export function layoutMessageBubble({
  props,
  messageArea,
  measurer,
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
    fontFamily: props.style.fontFamily,
    fontSize: props.style.fontSize,
    fontWeight: props.style.fontWeight,
    lineHeight: props.style.lineHeight,
    maxWidth: maxTextWidth,
    measurer,
  });
  const labelMeasurement = measureMessageLabel(props, measurer);
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
  const statusMeasurement = measureStatus(props);
  const hasTextContent = props.visibleText.length > 0;
  const hasMainContent = hasMedia || hasTextContent;
  const statusGap =
    statusMeasurement.visible && hasMainContent
      ? Math.max(2, Math.round(props.style.paddingY * 0.5))
      : 0;
  const contentWidth = Math.max(
    labelMeasurement.width,
    hasTextContent ? measurement.width : 0,
    mediaWidth,
    statusMeasurement.width,
  );
  const contentHeight =
    labelMeasurement.reserveHeight +
    mediaHeight +
    mediaTextGap +
    (hasTextContent ? measurement.height : 0) +
    statusGap +
    statusMeasurement.height;
  const bubbleWidth = Math.min(
    maxBubbleWidth,
    Math.max(
      props.style.paddingX * 2 + props.style.fontSize * 0.52,
      contentWidth + props.style.paddingX * 2,
    ),
  );
  const bubbleHeight = contentHeight + props.style.paddingY * 2;
  const alignment = props.layout.alignment;
  const tailReserve = tailProtrusion(props);
  const leftUnitReserve = Math.max(avatarReserve, tailReserve.left);
  const rightUnitReserve = tailReserve.right;
  const bubbleX =
    alignment === "right"
      ? messageArea.x + messageArea.width - rightUnitReserve - bubbleWidth
      : alignment === "center"
        ? messageArea.x + (messageArea.width - bubbleWidth) / 2
        : messageArea.x + leftUnitReserve;
  const roundedBubbleWidth = Math.round(bubbleWidth);
  const roundedBubbleHeight = Math.round(bubbleHeight);
  const roundedBubbleX =
    alignment === "right"
      ? Math.round(messageArea.x + messageArea.width) -
        Math.round(rightUnitReserve) -
        roundedBubbleWidth
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
    y: Math.round(
      bubbleBox.y +
        props.style.paddingY +
        labelMeasurement.reserveHeight +
        mediaHeight +
        mediaTextGap,
    ),
    width: Math.round(
      Math.max(0, bubbleBox.width - props.style.paddingX * 2),
    ),
    height: hasTextContent ? measurement.height : 0,
  };
  const statusBox = statusMeasurement.visible
    ? {
        x: Math.round(
          bubbleBox.x +
            bubbleBox.width -
            props.style.paddingX -
            statusMeasurement.width +
            statusMeasurement.offsetX,
        ),
        y: Math.round(
          textBox.y +
            textBox.height +
            statusGap +
            statusMeasurement.offsetY,
        ),
        width: Math.round(statusMeasurement.width),
        height: Math.round(statusMeasurement.height),
      }
    : undefined;
  const mediaBox = hasMedia
    ? {
        x: Math.round(bubbleBox.x + props.style.paddingX),
        y: Math.round(
          bubbleBox.y + props.style.paddingY + labelMeasurement.reserveHeight,
        ),
        width: Math.round(mediaWidth),
        height: Math.round(mediaHeight),
      }
    : undefined;
  const labelBox = labelMeasurement.visible
    ? {
        x: Math.round(bubbleBox.x + props.style.paddingX + labelMeasurement.offsetX),
        y: Math.round(bubbleBox.y + props.style.paddingY + labelMeasurement.offsetY),
        width: Math.round(labelMeasurement.width),
        height: Math.round(labelMeasurement.height),
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
    ...(labelBox ? { labelBox } : {}),
    ...(mediaBox ? { mediaBox } : {}),
    textBox,
    ...(statusBox ? { statusBox } : {}),
    ...(avatarBox ? { avatarBox } : {}),
    measurement,
    maxBubbleWidth,
    alignment,
  };
}
