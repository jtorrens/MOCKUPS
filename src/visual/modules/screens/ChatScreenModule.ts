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
import type { TextMeasurer } from "../../layout/types.js";
import type { RenderableNode } from "../../renderable/types.js";
import { ChatHeaderModule } from "../atomic/ChatHeaderModule.js";
import { KeyboardModule } from "../atomic/KeyboardModule.js";
import { renderMessageBubbleWithLayout } from "../atomic/MessageBubbleModule.js";
import { NavigationBarModule } from "../atomic/NavigationBarModule.js";
import { StatusBarModule } from "../atomic/StatusBarModule.js";
import { TextInputBarModule } from "../atomic/TextInputBarModule.js";
import type { VisualModule } from "../types.js";

function cssUrl(value: string) {
  return `url("${value.replace(/"/g, '\\"')}")`;
}

function clampOpacity(value: number) {
  return Math.max(0, Math.min(1, value));
}

function headerBackgroundColor(input: ResolvedChatScreenProps) {
  return (
    input.header.backgroundColor ??
    readString(
      input.theme.header,
      "background",
      readString(input.theme.colors, "background", "#FFFFFF"),
    )
  );
}

function createMessageBubbleInput(
  input: ResolvedChatScreenProps,
  message: ResolvedChatScreenProps["messages"][number],
) {
  const outgoing = message.direction === "outgoing";
  const system = message.direction === "system";
  const chatTokens = input.theme.chatBubbles;
  const tailTokens = readObject(chatTokens, "tail");
  const mediaTokens = readObject(chatTokens, "media");
  const avatarComponent = readObject(input.theme.components ?? {}, "avatar");
  const audioMessageComponent = readObject(
    input.theme.components ?? {},
    "audioMessage",
  );
  const videoMessageComponent = readObject(
    input.theme.components ?? {},
    "videoMessage",
  );
  const typographyTokens = input.theme.typography;
  const messageTypography = readObject(typographyTokens ?? {}, "message");
  const actorAvatar = outgoing
    ? input.ownerActor.avatar?.uri
    : input.header.avatar?.uri;
  const actorAvatarConfig = outgoing ? input.ownerActor.avatar : input.header.avatar;
  const mediaPlayStartFrame =
    typeof message.media?.playStartFrame === "number"
      ? message.media.playStartFrame
      : 0;
  const mediaStartFrame =
    typeof message.timing.writeOnStartFrame === "number" &&
    typeof message.timing.writeOnDurationFrames === "number"
      ? message.timing.writeOnStartFrame + message.timing.writeOnDurationFrames
      : message.timing.startFrame;
  const mediaFrame = Math.max(
    0,
    input.frame - mediaStartFrame - mediaPlayStartFrame,
  );

  return ResolvedMessageBubblePropsSchema.parse({
    frame: input.frame,
    fps: input.fps,
    id: message.id,
    direction: message.direction,
    text: message.text,
    visibleText: message.visibleText,
    status: message.status,
    actor: {
      id: message.sender.id,
      displayName: message.sender.displayName,
      ...(actorAvatar ? { avatarUri: actorAvatar } : {}),
      ...(typeof actorAvatarConfig?.scale === "number"
        ? { avatarScale: actorAvatarConfig.scale }
        : {}),
      ...(typeof actorAvatarConfig?.offsetX === "number"
        ? { avatarOffsetX: actorAvatarConfig.offsetX }
        : {}),
      ...(typeof actorAvatarConfig?.offsetY === "number"
        ? { avatarOffsetY: actorAvatarConfig.offsetY }
        : {}),
      ...(typeof actorAvatarConfig?.baseSize === "number"
        ? { avatarBaseSize: actorAvatarConfig.baseSize }
        : {}),
    },
    ...(message.media
      ? {
          media: {
            ...message.media,
            frame: mediaFrame,
          },
        }
      : {}),
    style: {
      ...(message.style ?? {
      backgroundColor: readString(
        chatTokens,
        outgoing
          ? "outgoingBackground"
          : system
            ? "systemBackground"
            : "incomingBackground",
        outgoing
          ? "#0B84FF"
          : system
            ? "rgba(118, 118, 128, 0.16)"
            : "#E9E9EB",
      ),
      textColor: readString(
        chatTokens,
        outgoing ? "outgoingText" : system ? "systemText" : "incomingText",
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
      tailVerticalPosition:
        readString(tailTokens, "verticalPosition", "bottom") === "top"
          ? "top"
          : "bottom",
      tailWidth: readNumber(tailTokens, "width", 0),
      tailHeight: readNumber(tailTokens, "height", 0),
      tailScale: readNumber(tailTokens, "scale", 1),
      shadowEnabled: chatTokens.shadowEnabled === true,
      shadow: readObject(chatTokens, "shadow"),
      surfaceRelief:
        chatTokens.surfaceReliefEnabled !== false
          ? readObject(input.theme.surfaceRelief ?? {}, "default")
          : {},
      avatarSize: readNumber(chatTokens, "avatarSize", 32),
      media: {
        borderWidth: readNumber(mediaTokens, "borderWidth", 0),
        cornerRadius: readNumber(
          mediaTokens,
          "cornerRadius",
          readNumber(chatTokens, "radius", 18),
        ),
        borderColor: readString(mediaTokens, "borderColor", "transparent"),
        shadowEnabled: mediaTokens.shadowEnabled === true,
      },
      }),
      audioMessage: audioMessageComponent,
      videoMessage: videoMessageComponent,
      avatar: {
        ...avatarComponent,
        ...(avatarComponent.surfaceReliefEnabled !== false
          ? {
              surfaceRelief: readObject(
                input.theme.surfaceRelief ?? {},
                "default",
              ),
            }
          : {}),
      },
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
      avatarGap: readNumber(chatTokens, "avatarGap", 8),
    },
    timing: {
      startFrame: message.timing.startFrame,
      enterDurationFrames: message.timing.enterDurationFrames,
      writeOnStartFrame: message.timing.writeOnStartFrame ?? null,
      writeOnDurationFrames: message.timing.writeOnDurationFrames ?? null,
      exitFrame: null,
    },
    animation: message.animation ?? {},
    ...(message.direction === "outgoing"
      ? {
          animation: {
            ...(message.animation ?? {}),
            cursor: {
              color: readString(input.theme.cursor, "color", "#007AFF"),
              width: readNumber(input.theme.cursor, "width", 2),
              blinkFrames: readNumber(input.theme.cursor, "blinkFrames", 15),
              visible:
                typeof message.timing.writeOnStartFrame === "number" &&
                typeof message.timing.writeOnDurationFrames === "number" &&
                message.visibleText.length > 0 &&
                message.visibleText.length < message.text.length,
            },
          },
        }
      : {}),
  });
}

function hasStartedAtFrame(message: ReturnType<typeof createMessageBubbleInput>) {
  return message.timing.startFrame <= message.frame;
}

export interface RenderChatScreenOptions {
  measurer?: TextMeasurer;
}

export const ChatScreenModule: VisualModule<ResolvedChatScreenProps> = {
  type: "chat_screen",
  version: 1,
  render(input, context): RenderableNode {
    const options = (context ?? {}) as RenderChatScreenOptions;
    const children: RenderableNode[] = [];
    const screenGutter = readNumber(input.theme.layout, "screenGutter", 24);
    const wallpaper = readObject(input.theme, "wallpaper");
    const wallpaperKind = readString(wallpaper, "kind", "solid");
    const wallpaperOpacity = clampOpacity(readNumber(wallpaper, "opacity", 1));
    const messageInputs = input.messages
      .map((message) => createMessageBubbleInput(input, message))
      .filter(hasStartedAtFrame)
      .filter((message) => {
        return typeof message.animation?.hideUntilWriteComplete === "boolean"
          ? message.animation.hideUntilWriteComplete !== true
          : true;
      });
    const layout = layoutChatScreen({
      props: input,
      messages: messageInputs,
      measurer: options.measurer,
    });
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

    if (layout.headerBox) {
      const statusBarHeight = layout.statusBarBox?.height ?? 0;
      children.push({
        id: `${input.screenInstanceId}:chat_header_bleed`,
        type: "chat_header_background",
        role: "conversation_header_background",
        frame: input.frame,
        box: {
          x: layout.headerBox.x,
          y: layout.rootBox.y,
          width: layout.headerBox.width,
          height: statusBarHeight + layout.headerBox.height,
        },
        style: {
          backgroundColor: headerBackgroundColor(input),
        },
        metadata: {
          layoutAffectsHeader: false,
          note: "Visual bleed fills behind the status bar without changing header layout.",
        },
      });
    }

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
            shadows: input.theme.shadows,
            surfaceRelief: input.theme.surfaceRelief,
            avatarComponent: readObject(input.theme.components ?? {}, "avatar"),
            buttonIconComponent: readObject(
              input.theme.components ?? {},
              "buttonIcon",
            ),
            typography: input.theme.typography,
            headerTokens: input.theme.header,
            screenGutter,
          }),
          box: layout.headerBox,
        },
      );
    }

    const messageNodes: RenderableNode[] = [];
    for (const messageInput of messageInputs) {
      const messageLayout = layout.messages.find(
        (candidate) => candidate.messageId === messageInput.id,
      );
      if (!messageLayout) {
        throw new Error(`Missing layout for message ${messageInput.id}`);
      }
      messageNodes.push(
        renderMessageBubbleWithLayout(messageInput, messageLayout.layout),
      );
    }
    children.push({
      id: `${input.screenInstanceId}:message_list`,
      type: "message_list",
      role: "messages",
      frame: input.frame,
      box: layout.messageListBox,
      style: {
        backgroundColor: "transparent",
        overflow: "clip",
      },
      children: messageNodes,
    });

    if (layout.textInputBarBox) {
      children.push({
        ...TextInputBarModule.render({
          frame: input.frame,
          viewport: input.viewport,
          textInputBar: input.textInputBar,
          tokens: input.theme,
        }),
        box: layout.textInputBarBox,
      });
    }

    if (layout.keyboardBox) {
      children.push({
        ...KeyboardModule.render({
          frame: input.frame,
          viewport: input.viewport,
          keyboard: input.keyboard,
          tokens: input.theme,
        }),
        box: layout.keyboardBox,
      });
    }

    if (layout.navigationBarBox) {
      children.push({
        ...NavigationBarModule.render({
          frame: input.frame,
          viewport: input.viewport,
          navigationBar: input.navigationBar,
          tokens: input.theme.navigationBar,
        }),
        box: layout.navigationBarBox,
      });
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
          messageAreaBox: layout.messageAreaBox,
          messageListBox: layout.messageListBox,
          textInputBarBox: layout.textInputBarBox,
          keyboardBox: layout.keyboardBox,
          navigationBarBox: layout.navigationBarBox,
          overflow: layout.overflow,
          strategy: "renderer_agnostic_chat_layout_v1",
          tokenSource: "theme.tokens_json.layout/header/messages",
        },
      },
    };
  },
};
