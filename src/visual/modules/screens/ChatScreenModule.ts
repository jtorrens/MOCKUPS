import {
  ResolvedMessageBubblePropsSchema,
  type ResolvedChatScreenProps,
} from "../../../domain/schemas/index.js";
import {
  readFontWeight,
  readNumber,
  readObject,
  readString,
} from "../../renderable/helpers.js";
import { layoutChatScreen } from "../../layout/index.js";
import type { RenderableNode } from "../../renderable/types.js";
import { ChatHeaderModule } from "../atomic/ChatHeaderModule.js";
import { renderMessageBubbleWithLayout } from "../atomic/MessageBubbleModule.js";
import { StatusBarModule } from "../atomic/StatusBarModule.js";
import type { VisualModule } from "../types.js";

function cssUrl(value: string) {
  return `url("${value.replace(/"/g, '\\"')}")`;
}

function clampOpacity(value: number) {
  return Math.max(0, Math.min(1, value));
}

function createMessageBubbleInput(
  input: ResolvedChatScreenProps,
  message: ResolvedChatScreenProps["messages"][number],
) {
  const outgoing = message.direction === "outgoing";
  const chatTokens = input.theme.chatBubbles;
  const tailTokens = readObject(chatTokens, "tail");
  const typographyTokens = input.theme.typography;
  const messageTypography = readObject(typographyTokens ?? {}, "message");
  const avatarTokens = input.theme.avatars;
  const actorAvatar = outgoing
    ? input.ownerActor.avatar?.uri
    : input.header.avatar?.uri;

  return ResolvedMessageBubblePropsSchema.parse({
    frame: input.frame,
    fps: input.fps,
    id: message.id,
    direction: message.direction,
    text: message.text,
    visibleText: message.visibleText,
    actor: {
      id: message.sender.id,
      displayName: message.sender.displayName,
      ...(actorAvatar ? { avatarUri: actorAvatar } : {}),
    },
    style: message.style ?? {
      backgroundColor: readString(
        chatTokens,
        outgoing ? "outgoingBackground" : "incomingBackground",
        outgoing ? "#0B84FF" : "#E9E9EB",
      ),
      textColor: readString(
        chatTokens,
        outgoing ? "outgoingText" : "incomingText",
        outgoing ? "#FFFFFF" : "#000000",
      ),
      fontFamily: readString(
        messageTypography,
        "fontFamily",
        readString(input.theme.fonts, "family", "system-ui"),
      ),
      fontSize: readNumber(
        messageTypography,
        "fontSize",
        readNumber(input.theme.fonts, "bodySize", 17),
      ),
      lineHeight: readNumber(
        messageTypography,
        "lineHeight",
        readNumber(input.theme.fonts, "bodyLineHeight", 21.25),
      ),
      fontWeight: readFontWeight(
        messageTypography,
        "fontWeight",
        readFontWeight(input.theme.fonts, "weight", "Regular"),
      ),
      borderRadius: readNumber(chatTokens, "radius", 18),
      paddingX: readNumber(chatTokens, "paddingX", 14),
      paddingY: readNumber(chatTokens, "paddingY", 9),
      tailStyle: readString(tailTokens, "style", "none"),
      tailWidth: readNumber(tailTokens, "width", 0),
      tailHeight: readNumber(tailTokens, "height", 0),
      shadow: readObject(chatTokens, "shadow"),
      avatarSize: readNumber(avatarTokens, "defaultSize", 32),
    },
    layout: message.layout ?? {
      maxWidth: Math.round(
        input.viewport.width * readNumber(chatTokens, "maxWidthRatio", 0.6667),
      ),
      alignment:
        message.direction === "system"
          ? "center"
          : outgoing
            ? "right"
            : "left",
      showTail: message.direction !== "system",
      groupPosition: "single",
      avatarGap: readNumber(avatarTokens, "gap", 8),
    },
    timing: {
      startFrame: message.timing.startFrame,
      enterDurationFrames: message.timing.enterDurationFrames,
      writeOnStartFrame: message.timing.writeOnStartFrame ?? null,
      writeOnDurationFrames: message.timing.writeOnDurationFrames ?? null,
      exitFrame: null,
    },
    animation: message.animation ?? {},
  });
}

export const ChatScreenModule: VisualModule<ResolvedChatScreenProps> = {
  type: "chat_screen",
  version: 1,
  render(input): RenderableNode {
    const children: RenderableNode[] = [];
    const screenGutter = readNumber(input.theme.layout, "screenGutter", 24);
    const wallpaper = readObject(input.theme, "wallpaper");
    const wallpaperKind = readString(wallpaper, "kind", "solid");
    const wallpaperOpacity = clampOpacity(readNumber(wallpaper, "opacity", 1));
    const messageInputs = input.messages.map((message) =>
      createMessageBubbleInput(input, message),
    );
    const layout = layoutChatScreen({ props: input, messages: messageInputs });
    const wallpaperImage = readObject(wallpaper, "image");
    const wallpaperUri = readString(wallpaperImage, "filePath", "");
    children.push({
      id: `${input.screenInstanceId}:wallpaper`,
      type: "wallpaper",
      role: "background",
      box: layout.rootBox,
      transform: { opacity: wallpaperOpacity },
      style:
        wallpaperKind === "image" && wallpaperUri
          ? {
              backgroundImage: cssUrl(wallpaperUri),
              backgroundSize: "cover",
              backgroundPosition: "center",
              backgroundRepeat: "no-repeat",
            }
          : {
              backgroundColor: readString(
                wallpaper,
                "color",
                readString(input.theme.colors, "background", "#FFFFFF"),
              ),
            },
    });
    if (layout.statusBarBox) {
      children.push(
        {
          ...StatusBarModule.render({
            frame: input.frame,
            viewport: input.viewport,
            statusBarHeight: input.device.statusBarHeight,
            statusBar: input.statusBar,
            tokens: input.theme.statusBar,
          }),
          box: layout.statusBarBox,
        },
      );
    }

    if (layout.headerBox) {
      children.push(
        {
          ...ChatHeaderModule.render({
            frame: input.frame,
            viewport: input.viewport,
            statusBarHeight: layout.statusBarBox?.height ?? 0,
            header: input.header,
            colors: input.theme.colors,
            fonts: input.theme.fonts,
            typography: input.theme.typography,
            headerTokens: input.theme.header,
            avatarTokens: input.theme.avatars,
            screenGutter,
          }),
          box: layout.headerBox,
        },
      );
    }

    for (const messageInput of messageInputs) {
      const messageLayout = layout.messages.find(
        (candidate) => candidate.messageId === messageInput.id,
      );
      if (!messageLayout) {
        throw new Error(`Missing layout for message ${messageInput.id}`);
      }
      children.push(
        renderMessageBubbleWithLayout(messageInput, messageLayout.layout),
      );
    }

    return {
      id: input.screenInstanceId,
      type: "chat_screen",
      role: "screen",
      frame: input.frame,
      box: layout.rootBox,
      style: {
        backgroundColor: readString(
          input.theme.colors,
          "background",
          "#FFFFFF",
        ),
        cornerRadius: input.device.cornerRadius,
        overflow: "clip",
      },
      children,
      metadata: {
        fps: input.fps,
        deviceId: input.device.id,
        ownerActorId: input.ownerActor.id,
        events: input.events,
        layout: {
          messageListBox: layout.messageListBox,
          overflow: layout.overflow,
          strategy: "renderer_agnostic_chat_layout_v1",
          tokenSource: "theme.tokens_json.layout/header/messages",
        },
      },
    };
  },
};
