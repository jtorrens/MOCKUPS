import { z } from "zod";
import {
  ResolvedMessageBubblePropsSchema,
  type ChatModuleMessage,
  type ResolvedMessageBubbleProps,
} from "../schemas/index.js";
import { clamp } from "./helpers.js";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

const MessageThemeSchema = z.object({
  fonts: z.object({
    family: z.string().min(1),
    bodySize: z.number().positive(),
    bodyLineHeight: z.number().positive(),
    weight: z.union([z.string().min(1), z.number().positive()]).optional(),
  }),
  typography: z
    .object({
      message: z
        .object({
          fontFamily: z.string().min(1).optional(),
          fontSize: z.number().positive().optional(),
          lineHeight: z.number().positive().optional(),
          fontWeight: z.union([z.string().min(1), z.number().positive()]).optional(),
        })
        .optional(),
    })
    .optional(),
  chatBubbles: z.object({
    outgoingBackground: z.string().min(1),
    outgoingText: z.string().min(1),
    incomingBackground: z.string().min(1),
    incomingText: z.string().min(1),
    systemBackground: z.string().min(1).optional(),
    systemText: z.string().min(1).optional(),
    paddingX: z.number().min(0),
    paddingY: z.number().min(0),
    maxWidthRatio: z.number().min(0).max(1),
    avatarSize: z.number().min(0).optional(),
    avatarGap: z.number().min(0).optional(),
    shadowEnabled: z.boolean().optional(),
    surfaceReliefEnabled: z.boolean().optional(),
    media: z
      .object({
        borderWidth: z.number().min(0).optional(),
        cornerRadius: z.number().min(0).optional(),
        borderColor: z.string().optional(),
        shadowEnabled: z.boolean().optional(),
      })
      .optional(),
    status: z
      .object({
        showText: z.boolean().optional(),
        showTicks: z.boolean().optional(),
        size: z.number().min(0).optional(),
        gap: z.number().min(0).optional(),
        offsetX: z.number().optional(),
        offsetY: z.number().optional(),
        tickSingleIconToken: z.string().optional(),
        tickDoubleIconToken: z.string().optional(),
        tickSingleIconUri: z.string().optional(),
        tickDoubleIconUri: z.string().optional(),
        textColor: z.string().optional(),
        sentColor: z.string().optional(),
        deliveredColor: z.string().optional(),
        readColor: z.string().optional(),
        failedColor: z.string().optional(),
      })
      .optional(),
    tail: z.object({
      style: z.string().min(1),
      verticalPosition: z.enum(["top", "bottom"]).optional(),
      width: z.number().min(0),
      height: z.number().min(0),
      scale: z.number().positive().optional(),
    }),
  }),
  shadows: z.record(z.string(), z.unknown()).optional(),
  surfaceRelief: z.record(z.string(), z.unknown()).optional(),
  radii: z.object({
    bubble: z.number().min(0),
  }),
  avatars: z.object({
    defaultSize: z.number().min(0),
    gap: z.number().min(0),
  }),
});

export interface ResolvedChatActor {
  id: string;
  displayName: string;
  avatarUri?: string;
  avatarScale?: number;
  avatarOffsetX?: number;
  avatarOffsetY?: number;
  avatarBaseSize?: number;
  color?: string;
  avatarTextColor?: string;
}

function messageStatus(message: ChatModuleMessage) {
  return isRecord(message.status)
    ? (message.status as Record<string, unknown>)
    : {};
}

function statusDeliveryValue(value: unknown) {
  return typeof value === "string" &&
    ["none", "sent", "delivered", "read", "failed"].includes(value)
    ? value
    : "none";
}

function resolveVisibleText(
  message: ChatModuleMessage,
  localFrame: number,
  direction: "incoming" | "outgoing" | "system",
): string {
  const text = message.text ?? "";
  if (direction === "system") {
    return text;
  }
  const reveal = message.textReveal;
  if (!reveal || reveal.mode === "none") {
    return text;
  }
  if (localFrame < reveal.startFrame) {
    return "";
  }
  if (reveal.durationFrames === 0) {
    return text;
  }

  const progress = clamp(
    (localFrame - reveal.startFrame) / reveal.durationFrames,
  );
  const characters = Array.from(text);
  return characters.slice(0, Math.floor(characters.length * progress)).join("");
}

function colorAlphaIsZero(value: string) {
  if (value.trim().toLowerCase() === "transparent") return true;
  const match = value
    .trim()
    .match(
      /^rgba\(\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*\d{1,3}\s*,\s*(0|0?\.0+)\s*\)$/i,
    );
  return match !== null;
}

export interface ResolveMessageBubbleInput {
  message: ChatModuleMessage;
  sender: ResolvedChatActor;
  direction: "incoming" | "outgoing" | "system";
  themeTokens: Record<string, unknown>;
  localFrame: number;
  fps: number;
  viewportWidth: number;
}

export function resolveMessageBubble({
  message,
  sender,
  direction,
  themeTokens: rawThemeTokens,
  localFrame,
  fps,
  viewportWidth,
}: ResolveMessageBubbleInput): ResolvedMessageBubbleProps {
  const themeTokens = MessageThemeSchema.parse(rawThemeTokens);
  const messageTypography = themeTokens.typography?.message;
  const isOutgoing = direction === "outgoing";
  const isSystem = direction === "system";
  const backgroundColor = isOutgoing
    ? themeTokens.chatBubbles.outgoingBackground
    : isSystem
      ? themeTokens.chatBubbles.systemBackground ?? "rgba(118, 118, 128, 0.16)"
      : themeTokens.chatBubbles.incomingBackground;
  const textColor = isOutgoing
    ? themeTokens.chatBubbles.outgoingText
    : isSystem
      ? themeTokens.chatBubbles.systemText ?? themeTokens.chatBubbles.incomingText
      : themeTokens.chatBubbles.incomingText;
  const systemTextOnly = isSystem && colorAlphaIsZero(backgroundColor);
  const rawStatus = messageStatus(message);
  const statusText = typeof rawStatus.text === "string" ? rawStatus.text : "";
  const deliveryStatus = statusDeliveryValue(rawStatus.deliveryStatus);
  const statusTokens = themeTokens.chatBubbles.status ?? {};
  const mediaTokens = themeTokens.chatBubbles.media ?? {};
  const messageStartFrame = message.startFrame ?? 0;
  const statusColor =
    deliveryStatus === "read"
      ? statusTokens.readColor
      : deliveryStatus === "failed"
        ? statusTokens.failedColor
        : deliveryStatus === "delivered"
          ? statusTokens.deliveredColor
          : statusTokens.sentColor;
  const enterProgress =
    message.enterDurationFrames === 0
      ? Number(localFrame >= messageStartFrame)
      : clamp(
          (localFrame - messageStartFrame) / message.enterDurationFrames,
        );
  const reveal = message.textReveal;
  const writeOnProgress =
    direction !== "system" && reveal?.mode === "simple_write_on"
      ? reveal.durationFrames === 0
        ? Number(localFrame >= reveal.startFrame)
        : clamp((localFrame - reveal.startFrame) / reveal.durationFrames)
      : 1;

  return ResolvedMessageBubblePropsSchema.parse({
    frame: localFrame,
    fps,
    id: message.id,
    direction,
    text: message.text ?? "",
    visibleText: resolveVisibleText(message, localFrame, direction),
    status: {
      text: statusText,
      deliveryStatus,
    },
    actor: {
      id: sender.id,
      displayName: sender.displayName,
      ...(sender.avatarUri ? { avatarUri: sender.avatarUri } : {}),
      ...(sender.avatarScale !== undefined ? { avatarScale: sender.avatarScale } : {}),
      ...(sender.avatarOffsetX !== undefined ? { avatarOffsetX: sender.avatarOffsetX } : {}),
      ...(sender.avatarOffsetY !== undefined ? { avatarOffsetY: sender.avatarOffsetY } : {}),
      ...(sender.avatarBaseSize !== undefined ? { avatarBaseSize: sender.avatarBaseSize } : {}),
      ...(sender.color ? { color: sender.color } : {}),
    },
    style: {
      backgroundColor,
      textColor,
      fontFamily: messageTypography?.fontFamily ?? themeTokens.fonts.family,
      fontSize: messageTypography?.fontSize ?? themeTokens.fonts.bodySize,
      lineHeight:
        messageTypography?.lineHeight ?? themeTokens.fonts.bodyLineHeight,
      fontWeight:
        messageTypography?.fontWeight ?? themeTokens.fonts.weight ?? "Regular",
      borderRadius: themeTokens.radii.bubble,
      paddingX: themeTokens.chatBubbles.paddingX,
      paddingY: themeTokens.chatBubbles.paddingY,
      tailStyle: themeTokens.chatBubbles.tail.style,
      tailVerticalPosition:
        themeTokens.chatBubbles.tail.verticalPosition ?? "bottom",
      tailWidth: themeTokens.chatBubbles.tail.width,
      tailHeight: themeTokens.chatBubbles.tail.height,
      tailScale: themeTokens.chatBubbles.tail.scale ?? 1,
      shadowEnabled:
        themeTokens.chatBubbles.shadowEnabled === true && !systemTextOnly,
      media: {
        borderWidth: mediaTokens.borderWidth ?? 0,
        cornerRadius: mediaTokens.cornerRadius ?? themeTokens.radii.bubble,
        borderColor: mediaTokens.borderColor ?? "transparent",
        shadowEnabled: mediaTokens.shadowEnabled === true,
      },
      status: {
        showText: statusTokens.showText !== false,
        showTicks: statusTokens.showTicks !== false,
        size: statusTokens.size ?? 11,
        gap: statusTokens.gap ?? 3,
        offsetX: statusTokens.offsetX ?? -8,
        offsetY: statusTokens.offsetY ?? -5,
        textColor: statusTokens.textColor ?? statusColor ?? themeTokens.chatBubbles.incomingText,
        tickColor: statusColor ?? statusTokens.textColor ?? themeTokens.chatBubbles.incomingText,
        tickSingleIconToken: statusTokens.tickSingleIconToken ?? "message_check",
        tickDoubleIconToken: statusTokens.tickDoubleIconToken ?? "message_done_all",
        tickSingleIconUri: statusTokens.tickSingleIconUri ?? "",
        tickDoubleIconUri: statusTokens.tickDoubleIconUri ?? "",
      },
      shadow: isRecord(themeTokens.shadows?.elevated)
        ? themeTokens.shadows.elevated
        : isRecord(themeTokens.shadows?.avatar)
          ? themeTokens.shadows.avatar
          : isRecord(themeTokens.shadows?.notification)
            ? themeTokens.shadows.notification
            : {},
      surfaceRelief:
        themeTokens.chatBubbles.surfaceReliefEnabled !== false &&
        isRecord(themeTokens.surfaceRelief?.default)
          ? themeTokens.surfaceRelief.default
          : {},
      avatarSize:
        themeTokens.chatBubbles.avatarSize ?? themeTokens.avatars.defaultSize,
      ...message.styleOverride,
    },
    layout: {
      maxWidth: Math.round(
        viewportWidth * themeTokens.chatBubbles.maxWidthRatio,
      ),
      alignment: isOutgoing
        ? "right"
        : direction === "system"
          ? "center"
          : "left",
      showTail: direction !== "system",
      groupPosition: "single",
      avatarGap: themeTokens.chatBubbles.avatarGap ?? themeTokens.avatars.gap,
      ...message.layoutOverride,
    },
    timing: {
      startFrame: messageStartFrame,
      enterDurationFrames: message.enterDurationFrames,
      writeOnStartFrame:
        direction !== "system" && reveal?.mode === "simple_write_on"
          ? reveal.startFrame
          : null,
      writeOnDurationFrames:
        direction !== "system" && reveal?.mode === "simple_write_on"
          ? reveal.durationFrames
          : null,
      exitFrame: message.exitFrame ?? null,
    },
    animation: {
      enter: {
        type: "linear",
        progress: enterProgress,
        translateY: Math.round((1 - enterProgress) * 12),
        opacity: enterProgress,
      },
      writeOnProgress,
      ...message.animation,
    },
  });
}
