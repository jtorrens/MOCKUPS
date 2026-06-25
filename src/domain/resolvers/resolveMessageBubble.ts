import { z } from "zod";
import {
  ResolvedMessageBubblePropsSchema,
  type ChatModuleMessage,
  type ResolvedMessageBubbleProps,
} from "../schemas/index.js";
import { clamp } from "./helpers.js";

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
    paddingX: z.number().min(0),
    paddingY: z.number().min(0),
    maxWidthRatio: z.number().min(0).max(1),
    tail: z.object({
      style: z.string().min(1),
      width: z.number().min(0),
      height: z.number().min(0),
    }),
    shadow: z.record(z.string(), z.unknown()),
  }),
  radii: z.object({
    bubble: z.number().min(0),
  }),
  avatars: z.object({
    defaultSize: z.number().min(0),
    gap: z.number().min(0),
  }),
});

export interface ResolvedChatParticipant {
  participantId: string;
  actorId?: string;
  displayName: string;
  avatarUri?: string;
  role: "owner" | "participant";
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

export interface ResolveMessageBubbleInput {
  message: ChatModuleMessage;
  sender: ResolvedChatParticipant;
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
  const enterProgress =
    message.enterDurationFrames === 0
      ? Number(localFrame >= message.startFrame)
      : clamp(
          (localFrame - message.startFrame) / message.enterDurationFrames,
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
    actor: {
      id: sender.actorId ?? sender.participantId,
      displayName: sender.displayName,
      ...(sender.avatarUri ? { avatarUri: sender.avatarUri } : {}),
    },
    style: {
      backgroundColor: isOutgoing
        ? themeTokens.chatBubbles.outgoingBackground
        : themeTokens.chatBubbles.incomingBackground,
      textColor: isOutgoing
        ? themeTokens.chatBubbles.outgoingText
        : themeTokens.chatBubbles.incomingText,
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
      tailWidth: themeTokens.chatBubbles.tail.width,
      tailHeight: themeTokens.chatBubbles.tail.height,
      shadow: themeTokens.chatBubbles.shadow,
      avatarSize: themeTokens.avatars.defaultSize,
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
      avatarGap: themeTokens.avatars.gap,
      ...message.layoutOverride,
    },
    timing: {
      startFrame: message.startFrame,
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
