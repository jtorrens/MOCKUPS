import type { ResolvedMessageBubbleProps } from "../../../domain/schemas/index.js";
import {
  layoutMessageBubble,
  type MessageBubbleLayout,
} from "../../layout/index.js";
import type { RenderableNode } from "../../renderable/types.js";
import type { VisualModule } from "../types.js";
import { AvatarModule } from "./AvatarModule.js";

function animationOpacity(
  animation: Record<string, unknown>,
): number | undefined {
  const enter = animation.enter;
  if (typeof enter !== "object" || enter === null || !("opacity" in enter)) {
    return undefined;
  }
  return typeof enter.opacity === "number" ? enter.opacity : undefined;
}

function cursorBlinkOpacity(frame: number, blinkFrames: number) {
  const cycle = Math.max(1, blinkFrames) * 4;
  return frame % cycle < Math.max(1, blinkFrames) * 3 ? 1 : 0.28;
}

function readRecord(value: unknown): Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value)
    ? (value as Record<string, unknown>)
    : {};
}

function readString(value: unknown, fallback = "") {
  return typeof value === "string" ? value : fallback;
}

function readNumber(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function colorAlphaIsZero(value: string) {
  if (value.trim().toLowerCase() === "transparent") return true;
  return (
    value
      .trim()
      .match(
        /^rgba\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*(0|0?\.0+)\s*\)$/i,
      ) !== null
  );
}

function maskUrl(value: string) {
  return value ? `url("${value.replace(/"/g, '\\"')}")` : undefined;
}

function messageStatusNode(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode | undefined {
  const status = readRecord(input.status);
  const statusStyle = readRecord(input.style.status);
  const deliveryStatus = readString(status.deliveryStatus, "none");
  const text = readString(status.text);
  const showText = statusStyle.showText !== false && text.trim().length > 0;
  const showTicks =
    input.direction === "outgoing" &&
    statusStyle.showTicks !== false &&
    deliveryStatus !== "none";
  if (!showText && !showTicks) return undefined;

  const size = readNumber(statusStyle.size, 11);
  const gap = readNumber(statusStyle.gap, 3);
  const offsetX = readNumber(statusStyle.offsetX, -8);
  const offsetY = readNumber(statusStyle.offsetY, -5);
  const lineHeight = Math.round(size * 1.15);
  const textWidth = showText ? Math.ceil(Array.from(text).length * size * 0.54) : 0;
  const isDoubleTick = deliveryStatus === "delivered" || deliveryStatus === "read";
  const tickWidth = showTicks ? Math.ceil(size * (isDoubleTick ? 1.45 : 1)) : 0;
  const totalWidth =
    textWidth + tickWidth + (showText && showTicks ? Math.max(0, gap) : 0);
  const statusBox = {
    x: Math.round(layout.bubbleBox.x + layout.bubbleBox.width + offsetX - totalWidth),
    y: Math.round(layout.bubbleBox.y + layout.bubbleBox.height + offsetY - lineHeight),
    width: Math.max(1, totalWidth),
    height: lineHeight,
  };
  const children: RenderableNode[] = [];
  let cursorX = statusBox.x;
  if (showText) {
    children.push({
      id: `${input.id}:status:text`,
      type: "message_bubble_status_text",
      role: "status_text",
      frame: input.frame,
      box: {
        x: cursorX,
        y: statusBox.y,
        width: Math.max(1, textWidth),
        height: lineHeight,
      },
      text,
      style: {
        color: readString(statusStyle.textColor, input.style.textColor),
        fontFamily: input.style.fontFamily,
        fontSize: size,
        lineHeight,
        fontWeight: input.style.fontWeight,
      },
    });
    cursorX += textWidth + (showTicks ? Math.max(0, gap) : 0);
  }
  if (showTicks) {
    const iconUri = readString(
      isDoubleTick ? statusStyle.tickDoubleIconUri : statusStyle.tickSingleIconUri,
    );
    const token = readString(
      isDoubleTick ? statusStyle.tickDoubleIconToken : statusStyle.tickSingleIconToken,
      isDoubleTick ? "message_done_all" : "message_check",
    );
    children.push({
      id: `${input.id}:status:ticks`,
      type: "message_bubble_status_icon",
      role: deliveryStatus,
      frame: input.frame,
      box: {
        x: cursorX,
        y: statusBox.y + Math.round((lineHeight - size) / 2),
        width: Math.max(1, tickWidth),
        height: size,
      },
      text: isDoubleTick ? "✓✓" : "✓",
      style: {
        color: readString(statusStyle.tickColor, input.style.textColor),
        fontSize: size,
        lineHeight: size,
        ...(iconUri
          ? {
              maskImage: maskUrl(iconUri),
              WebkitMaskImage: maskUrl(iconUri),
            }
          : {}),
      },
      metadata: {
        token,
        deliveryStatus,
      },
    });
  }
  return {
    id: `${input.id}:status`,
    type: "message_bubble_status",
    role: deliveryStatus,
    frame: input.frame,
    box: statusBox,
    style: {
      backgroundColor: "transparent",
      overflow: "visible",
    },
    children,
    metadata: {
      anchor: "bubble.bottomRight",
      offsetX,
      offsetY,
      deliveryStatus,
    },
  };
}

function tailNode(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode | undefined {
  if (!input.layout.showTail || input.direction === "system") return undefined;
  if (input.style.tailStyle === "none") return undefined;
  const scale = Math.max(0.01, input.style.tailScale);
  const width = Math.round(input.style.tailWidth * scale);
  const height = Math.round(input.style.tailHeight * scale);
  if (width <= 0 || height <= 0) return undefined;
  const side = input.direction === "outgoing" ? "right" : "left";
  const vertical = input.style.tailVerticalPosition;
  const alignTailToBubbleEdge = input.style.tailStyle === "cut_corner";
  const x =
    side === "right"
      ? layout.bubbleBox.x + layout.bubbleBox.width - Math.ceil(width * 0.34)
      : layout.bubbleBox.x - Math.floor(width * 0.66);
  const y =
    vertical === "top"
      ? alignTailToBubbleEdge
        ? layout.bubbleBox.y
        : layout.bubbleBox.y + Math.round(input.style.borderRadius * 0.35)
      : alignTailToBubbleEdge
        ? layout.bubbleBox.y + layout.bubbleBox.height - height
        : layout.bubbleBox.y +
          layout.bubbleBox.height -
          height -
          Math.round(input.style.borderRadius * 0.18);
  return {
    id: `${input.id}:tail`,
    type: "message_bubble_tail",
    role: `${side}_${vertical}`,
    frame: input.frame,
    box: { x, y, width, height },
    style: {
      backgroundColor: input.style.backgroundColor,
      tailStyle: input.style.tailStyle,
      side,
      vertical,
      borderRadius: Math.max(1, Math.round(input.style.borderRadius * 0.35)),
    },
    metadata: {
      side,
      vertical,
      scale,
      style: input.style.tailStyle,
    },
  };
}

export function renderMessageBubbleWithLayout(
  input: ResolvedMessageBubbleProps,
  layout: MessageBubbleLayout,
): RenderableNode {
    const hasShapeShadow =
      input.style.shadowEnabled && !colorAlphaIsZero(input.style.backgroundColor);
    const cursorConfig =
      typeof input.animation.cursor === "object" && input.animation.cursor !== null
        ? (input.animation.cursor as Record<string, unknown>)
        : {};
    const cursorVisible = cursorConfig.visible === true;
    const cursorBlinkFrames =
      typeof cursorConfig.blinkFrames === "number" && cursorConfig.blinkFrames > 0
        ? cursorConfig.blinkFrames
        : 15;
    const cursorOpacity = cursorBlinkOpacity(input.frame, cursorBlinkFrames);
    const tail = tailNode(input, layout);
    const shapeChildren: RenderableNode[] = [
      {
        id: `${input.id}:shape:body`,
        type: "message_bubble_body",
        role: input.direction,
        frame: input.frame,
        box: {
          x: layout.bubbleBox.x,
          y: layout.bubbleBox.y,
          width: layout.bubbleBox.width,
          height: layout.bubbleBox.height,
        },
        style: {
          backgroundColor: input.style.backgroundColor,
          borderRadius: input.style.borderRadius,
        },
      },
    ];
    if (tail) {
      shapeChildren.push(tail);
    }
    const children: RenderableNode[] = [
      {
        id: `${input.id}:shape`,
        type: "message_bubble_shape",
        role: input.direction,
        frame: input.frame,
        box: {
          x: layout.bubbleBox.x,
          y: layout.bubbleBox.y,
          width: layout.bubbleBox.width,
          height: layout.bubbleBox.height,
        },
        style: {
          shadow: hasShapeShadow ? input.style.shadow : {},
        },
        children: shapeChildren,
      },
    ];
    if (input.actor.avatarUri && layout.avatarBox) {
      children.push(
        {
          ...AvatarModule.render({
            id: `${input.id}:avatar`,
            uri: input.actor.avatarUri,
            size: input.style.avatarSize,
            label: input.actor.displayName,
            frame: input.frame,
            ...(input.style.shadowEnabled ? { shadow: input.style.shadow } : {}),
          }),
          box: layout.avatarBox,
        },
      );
    }
    const status = messageStatusNode(input, layout);
    if (status) {
      children.push(status);
    }
    children.push({
      id: `${input.id}:text`,
      type: "text",
      role: "message_text",
      frame: input.frame,
      box: layout.textBox,
      text: input.visibleText,
      style: {
        color: input.style.textColor,
        fontFamily: input.style.fontFamily,
        fontSize: input.style.fontSize,
        lineHeight: input.style.lineHeight,
        fontWeight: input.style.fontWeight,
        ...(input.direction === "system" ? { textAlign: "center" } : {}),
      },
      children:
        cursorVisible && input.direction === "outgoing"
          ? [
              {
                id: `${input.id}:text:cursor`,
                type: "message_text_cursor",
                role: "cursor",
                frame: input.frame,
                style: {
                  background:
                    typeof cursorConfig.color === "string"
                      ? cursorConfig.color
                      : input.style.textColor,
                  width:
                    typeof cursorConfig.width === "number"
                      ? cursorConfig.width
                      : 2,
                  opacity: cursorOpacity,
                },
              },
            ]
          : undefined,
    });

    return {
      id: input.id,
      type: "message_bubble",
      role: input.direction,
      frame: input.frame,
      box: layout.bubbleBox,
      transform: {
        opacity: animationOpacity(input.animation),
      },
      style: {
        backgroundColor: "transparent",
        textColor: input.style.textColor,
        fontFamily: input.style.fontFamily,
        fontSize: input.style.fontSize,
        lineHeight: input.style.lineHeight,
        fontWeight: input.style.fontWeight,
        borderRadius: input.style.borderRadius,
        paddingX: input.style.paddingX,
        paddingY: input.style.paddingY,
        showTail: input.layout.showTail,
        tailStyle: input.style.tailStyle,
        tailVerticalPosition: input.style.tailVerticalPosition,
        tailWidth: input.style.tailWidth,
        tailHeight: input.style.tailHeight,
        tailScale: input.style.tailScale,
        shadow: {},
      },
      text: input.visibleText,
      children,
      metadata: {
        actorId: input.actor.id,
        fullText: input.text,
        timing: input.timing,
        animation: input.animation,
        layoutIntent: input.layout,
        avatarGap: input.layout.avatarGap,
        tailGeometry: {
          style: input.style.tailStyle,
          width: input.style.tailWidth,
          height: input.style.tailHeight,
          verticalPosition: input.style.tailVerticalPosition,
          scale: input.style.tailScale,
          path: "not_computed_renderer_agnostic_stub",
        },
        measurement: layout.measurement,
        maxBubbleWidth: layout.maxBubbleWidth,
        alignment: layout.alignment,
        tokenSources: {
          style: "theme.tokens_json.chatBubbles/typography",
          layout: "theme.tokens_json.chatBubbles",
        },
      },
    };
}

export const MessageBubbleModule: VisualModule<ResolvedMessageBubbleProps> = {
  type: "message_bubble",
  version: 1,
  render(input): RenderableNode {
    const avatarReserve =
      input.direction === "incoming" && input.actor.avatarUri
        ? input.style.avatarSize + input.layout.avatarGap
        : 0;
    const layout = layoutMessageBubble({
      props: input,
      messageArea: {
        x: 0,
        y: 0,
        width: input.layout.maxWidth + avatarReserve,
        height: Number.MAX_SAFE_INTEGER,
      },
      y: 0,
    });
    return renderMessageBubbleWithLayout(input, layout);
  },
};
