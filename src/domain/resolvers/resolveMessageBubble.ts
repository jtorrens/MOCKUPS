import { z } from "zod";
import {
  ResolvedMessageBubblePropsSchema,
  type ChatModuleMessage,
  type ResolvedMessageBubbleProps,
} from "../schemas/index.js";
import { fontWeightForProductionStyle } from "../fonts/productionFontNormalization.js";
import { surfaceStyleNormalize } from "../value-system/index.js";
import { clamp } from "./helpers.js";

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function numberValue(value: unknown, fallback: number) {
  return typeof value === "number" && Number.isFinite(value) ? value : fallback;
}

function stringValue(value: unknown, fallback: string) {
  return typeof value === "string" && value.trim() ? value : fallback;
}

function nestedValue(root: Record<string, unknown>, path: readonly string[]) {
  let current: unknown = root;
  for (const part of path) {
    if (!isRecord(current)) return undefined;
    current = current[part];
  }
  return current;
}

function themeColor(
  themeTokens: Record<string, unknown>,
  palette: Map<string, string>,
  token: string,
  fallback: string,
) {
  const colors = isRecord(themeTokens.colors) ? themeTokens.colors : {};
  const colorReference =
    typeof colors[token] === "string"
      ? colors[token]
      : token.includes(".")
        ? nestedValue(themeTokens, token.split("."))
        : undefined;
  if (typeof colorReference === "string") {
    return palette.get(colorReference) ?? colorReference;
  }
  return palette.get(token) ?? fallback;
}

function themeRadius(
  themeTokens: Record<string, unknown>,
  token: unknown,
  fallback: number,
) {
  const radii = isRecord(themeTokens.radii) ? themeTokens.radii : {};
  const key = stringValue(token, "");
  const scopedKey = key.startsWith("radii.") ? key.slice("radii.".length) : key;
  const value =
    typeof radii[scopedKey] === "number"
      ? radii[scopedKey]
      : key.includes(".")
        ? nestedValue(themeTokens, key.split("."))
        : undefined;
  return numberValue(value, fallback);
}

function resolvedFontWeight(value: unknown) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  if (typeof value === "string" && value.trim()) {
    const parsed = Number(value);
    if (Number.isFinite(parsed)) return parsed;
    return fontWeightForProductionStyle(value);
  }
  return undefined;
}

function cssFontFamilyStack(primary: string, emojiFamily?: string) {
  const trimmedPrimary = primary.trim();
  const trimmedEmoji = emojiFamily?.trim();
  if (!trimmedEmoji || trimmedEmoji === trimmedPrimary) return trimmedPrimary;
  return `"${trimmedPrimary.replace(/"/g, '\\"')}", "${trimmedEmoji.replace(/"/g, '\\"')}"`;
}

const MessageThemeSchema = z.object({
  colors: z.record(z.string(), z.unknown()).optional(),
  fonts: z.object({
    family: z.string().min(1),
    emojiFamily: z.string().min(1).optional(),
    bodySize: z.number().positive(),
    bodyLineHeight: z.number().positive(),
    weight: z.union([z.string().min(1), z.number().positive()]).optional(),
    fontWeight: z.union([z.string().min(1), z.number().positive()]).optional(),
    fontStyle: z.enum(["normal", "italic"]).optional(),
  }),
  typography: z
    .object({
      message: z
        .object({
          fontFamily: z.string().min(1).optional(),
          fontStyle: z.enum(["normal", "italic"]).optional(),
          fontSize: z.number().positive().optional(),
          lineHeight: z.number().positive().optional(),
          fontWeight: z.union([z.string().min(1), z.number().positive()]).optional(),
        })
        .optional(),
    })
    .optional(),
  components: z.record(z.string(), z.unknown()).optional(),
  chatBubbles: z.object({
    outgoingBackground: z.string().min(1),
    outgoingText: z.string().min(1),
    incomingBackground: z.string().min(1),
    incomingText: z.string().min(1),
    systemBackground: z.string().min(1).optional(),
    systemText: z.string().min(1).optional(),
    paddingX: z.number().min(0),
    paddingY: z.number().min(0),
    contentMetaGap: z.number().min(0).optional(),
    maxWidthRatio: z.number().min(0).max(1),
    avatarSize: z.number().min(0).optional(),
    avatarGap: z.number().min(0).optional(),
    style: z.record(z.string(), z.unknown()).optional(),
    shadowEnabled: z.boolean().optional(),
    surfaceReliefEnabled: z.boolean().optional(),
    avatar: z.record(z.string(), z.unknown()).optional(),
    messageLabelStyle: z.record(z.string(), z.unknown()).optional(),
    media: z
      .object({
        borderWidth: z.number().min(0).optional(),
        cornerRadius: z.number().min(0).optional(),
        borderColor: z.string().optional(),
        shadowEnabled: z.boolean().optional(),
        surfaceReliefEnabled: z.boolean().optional(),
        style: z.record(z.string(), z.unknown()).optional(),
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
    }),
  }),
  shadows: z.record(z.string(), z.unknown()).optional(),
  surfaceRelief: z.record(z.string(), z.unknown()).optional(),
  radii: z.object({
    control: z.number().min(0),
    card: z.number().min(0),
    panel: z.number().min(0),
    surface: z.number().min(0),
    pill: z.number().min(0),
    avatar: z.number().min(0),
    full: z.number().min(0),
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

export type TimedChatModuleMessage = ChatModuleMessage & {
  startFrame: number;
};

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
  message: TimedChatModuleMessage;
  sender: ResolvedChatActor;
  direction: "incoming" | "outgoing" | "system";
  themeTokens: Record<string, unknown>;
  palette: Map<string, string>;
  localFrame: number;
  fps: number;
  viewportWidth: number;
}

export function resolveMessageBubble({
  message,
  sender,
  direction,
  themeTokens: rawThemeTokens,
  palette,
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
  const bubbleStyleTokens = surfaceStyleNormalize(themeTokens.chatBubbles.style);
  const mediaStyleTokens = surfaceStyleNormalize(mediaTokens.style);
  const avatarStyleTokens = surfaceStyleNormalize({
    cornerRadiusToken: "radii.avatar",
    ...(isRecord(themeTokens.chatBubbles.avatar?.style)
      ? themeTokens.chatBubbles.avatar.style
      : {}),
  });
  const avatarTokens = isRecord(themeTokens.chatBubbles.avatar)
    ? themeTokens.chatBubbles.avatar
    : {};
  const labelStyleTokens = surfaceStyleNormalize(
    themeTokens.chatBubbles.messageLabelStyle,
  );
  const themeSurfaceRelief = isRecord(themeTokens.surfaceRelief?.default)
    ? themeTokens.surfaceRelief.default
    : {};
  const avatarBorderColor = themeColor(
    themeTokens,
    palette,
    stringValue(avatarStyleTokens.borderColorToken, "borders.primary"),
    "transparent",
  );
  const labelBorderColor = themeColor(
    themeTokens,
    palette,
    stringValue(labelStyleTokens.borderColorToken, "borders.primary"),
    "transparent",
  );
  const mediaBorderColor = themeColor(
    themeTokens,
    palette,
    stringValue(mediaStyleTokens.borderColorToken, "borders.primary"),
    mediaTokens.borderColor ?? "transparent",
  );
  const componentTokens = themeTokens.components ?? {};
  const avatarComponent = isRecord(componentTokens.avatar)
    ? componentTokens.avatar
    : {};
  const messageStartFrame = message.startFrame;
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
  const messageFontFamily = messageTypography?.fontFamily ?? themeTokens.fonts.family;

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
      fontFamily: cssFontFamilyStack(messageFontFamily, themeTokens.fonts.emojiFamily),
      fontStyle: messageTypography?.fontStyle ?? themeTokens.fonts.fontStyle,
      fontSize: messageTypography?.fontSize ?? themeTokens.fonts.bodySize,
      lineHeight:
        messageTypography?.lineHeight ?? themeTokens.fonts.bodyLineHeight,
      fontWeight:
        resolvedFontWeight(messageTypography?.fontWeight) ??
        resolvedFontWeight(themeTokens.fonts.fontWeight) ??
        resolvedFontWeight(themeTokens.fonts.weight) ??
        400,
      borderRadius: themeRadius(
        themeTokens,
        bubbleStyleTokens.cornerRadiusToken,
        18,
      ),
      borderColor: themeColor(
        themeTokens,
        palette,
        stringValue(bubbleStyleTokens.borderColorToken, "borders.primary"),
        "transparent",
      ),
      borderWidth: numberValue(bubbleStyleTokens.borderWidth, 0),
      paddingX: themeTokens.chatBubbles.paddingX,
      paddingY: themeTokens.chatBubbles.paddingY,
      contentMetaGap: themeTokens.chatBubbles.contentMetaGap ?? 4,
      tailStyle: themeTokens.chatBubbles.tail.style,
      tailVerticalPosition:
        themeTokens.chatBubbles.tail.verticalPosition ?? "bottom",
      tailWidth: themeTokens.chatBubbles.tail.width,
      tailHeight: themeTokens.chatBubbles.tail.height,
      shadowEnabled:
        bubbleStyleTokens.shadowEnabled === true && !systemTextOnly,
      media: {
        borderWidth: numberValue(mediaStyleTokens.borderWidth, 0),
        cornerRadius: themeRadius(
          themeTokens,
          mediaStyleTokens.cornerRadiusToken,
          18,
        ),
        borderColor: mediaBorderColor,
        shadowEnabled: mediaStyleTokens.shadowEnabled === true,
        surfaceReliefEnabled: mediaStyleTokens.surfaceReliefEnabled === true,
        surfaceRelief: isRecord(mediaStyleTokens.surfaceRelief)
          ? mediaStyleTokens.surfaceRelief
          : {},
      },
      avatar: {
        ...avatarComponent,
        ...avatarStyleTokens,
        cornerRadius: themeRadius(
          themeTokens,
          avatarStyleTokens.cornerRadiusToken,
          numberValue(avatarComponent.cornerRadius, 0),
        ),
        alignment:
          avatarTokens.alignment === "top" || avatarTokens.alignment === "center"
            ? avatarTokens.alignment
            : "bottom",
        offsetX: numberValue(avatarTokens.offsetX, 0),
        offsetY: numberValue(avatarTokens.offsetY, 0),
        borderColor: avatarBorderColor,
        surfaceRelief:
          avatarStyleTokens.surfaceReliefEnabled === true &&
          isRecord(avatarStyleTokens.surfaceRelief)
            ? avatarStyleTokens.surfaceRelief
            : avatarComponent.surfaceRelief,
      },
      label: {
        ...labelStyleTokens,
        cornerRadius: themeRadius(
          themeTokens,
          labelStyleTokens.cornerRadiusToken,
          0,
        ),
        borderColor: labelBorderColor,
        surfaceRelief:
          labelStyleTokens.surfaceReliefEnabled === true &&
          isRecord(labelStyleTokens.surfaceRelief)
            ? labelStyleTokens.surfaceRelief
            : undefined,
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
        bubbleStyleTokens.surfaceReliefEnabled === true
          ? isRecord(bubbleStyleTokens.surfaceRelief)
            ? bubbleStyleTokens.surfaceRelief
            : themeSurfaceRelief
          : {},
      avatarSize:
        themeTokens.chatBubbles.avatarSize ?? 32,
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
      avatarGap: themeTokens.chatBubbles.avatarGap ?? 8,
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
