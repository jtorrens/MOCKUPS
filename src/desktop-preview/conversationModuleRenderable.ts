import type { RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { bubbleComponentToRenderable } from "./bubbleComponentRenderable.js";
import { resolveBubbleComponent } from "./bubbleComponentResolver.js";
import { componentPresetConfig } from "./componentPreviewDefaults.js";
import {
  asRecord,
  optionalBoolean,
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredString,
} from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { keyboardComponentToRenderable } from "./keyboardComponentRenderable.js";
import { resolveKeyboardComponent } from "./keyboardComponentResolver.js";
import { navigationBarComponentToRenderable } from "./navigationBarComponentRenderable.js";
import { resolveNavigationBarComponent } from "./navigationBarComponentResolver.js";
import {
  numberToken,
  previewScreenBox,
  renderableVisualBounds,
  renderScale,
  selectedColor,
  selectedPaletteColor,
  translateRenderableNode,
} from "./componentRenderableCommon.js";
import { mediaFrameUriForPath } from "./previewAssetResolver.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";
import { textInputBarComponentToRenderable } from "./textInputBarComponentRenderable.js";
import { resolveTextInputBarComponent } from "./textInputBarComponentResolver.js";
import { motionFrameProgress, requiredMotionContract } from "./previewMotionHelpers.js";

type JsonRecord = Record<string, unknown>;

export function conversationModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const config = parseObject(payload.configJson);
  const preview = runtimePreview(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const conversation = {
    ...asRecord(config.conversation),
    ...asRecord(parseObject(payload.instanceJson).behavior),
  };
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const children: RenderableNode[] = [];
  const wallpaper = requiredBoolean(
    conversation,
    "useAppWallpaper",
    "module.conversation.useAppWallpaper",
  )
    ? appWallpaperNode(payload, screen)
    : undefined;
  if (wallpaper) children.push(wallpaper);

  const status = requiredBoolean(
    conversation,
    "showStatusBar",
    "module.conversation.showStatusBar",
  )
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "status_bar",
        requiredString(conversation, "statusBarVariant", "module.conversation.statusBarVariant"),
        {},
        (childPayload) =>
          statusBarComponentToRenderable(childPayload, resolveStatusBarComponent(childPayload)),
      )
    : undefined;
  const navigation = requiredBoolean(
    conversation,
    "showNavigationBar",
    "module.conversation.showNavigationBar",
  )
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "navigation_bar",
        requiredString(
          conversation,
          "navigationBarVariant",
          "module.conversation.navigationBarVariant",
        ),
        {},
        (childPayload) =>
          navigationBarComponentToRenderable(
            childPayload,
            resolveNavigationBarComponent(childPayload),
          ),
      )
    : undefined;
  const conversationFrame = Math.max(0, Math.floor(optionalNumber(preview, "conversationFrame", Number.MAX_SAFE_INTEGER)));
  const composer = composerState(instanceMessages(preview), conversationFrame);
  const keyboardVisible = composer.keyboardVisible
    && requiredBoolean(conversation, "showKeyboard", "module.conversation.showKeyboard");
  const textInputVisible = composer.textInputVisible
    && requiredBoolean(conversation, "showTextInputBar", "module.conversation.showTextInputBar");
  const keyboard = keyboardVisible
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "keyboard",
        requiredString(conversation, "keyboardVariant", "module.conversation.keyboardVariant"),
        {
          text: composer.text,
          currentCharacter: 1,
        },
        (childPayload) =>
          keyboardComponentToRenderable(childPayload, resolveKeyboardComponent(childPayload)),
      )
    : undefined;
  const textInput = textInputVisible
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "textInputBar",
        requiredString(
          conversation,
          "textInputBarVariant",
          "module.conversation.textInputBarVariant",
        ),
        {
          sampleText: composer.text,
          availableWidth: screen.width / scale,
        },
        (childPayload) =>
          textInputBarComponentToRenderable(
            childPayload,
            resolveTextInputBarComponent(childPayload),
          ),
      )
    : undefined;

  const navHeight = navigation?.box?.height ?? 0;
  const keyboardTargetY = screen.y + screen.height - navHeight - (keyboard?.box?.height ?? 0);
  const keyboardNode = keyboard?.box
    ? translateRenderableNode(keyboard, { x: 0, y: keyboardTargetY - keyboard.box.y })
    : keyboard;
  const keyboardBounds = keyboardNode ? renderableVisualBounds(keyboardNode) : undefined;
  const keyboardHeight = keyboardBounds?.height ?? 0;
  const textInputTargetY = screen.y + screen.height - navHeight - keyboardHeight - (textInput?.box?.height ?? 0);
  const textInputNode = textInput?.box
    ? translateRenderableNode(textInput, { x: 0, y: textInputTargetY - textInput.box.y })
    : textInput;

  const header = requiredBoolean(
    conversation,
    "showHeader",
    "module.conversation.showHeader",
  )
    ? headerNode(
        payload,
        componentBaseConfigs,
        conversation,
        preview,
        (status?.box?.height ?? 0),
        requiredNumber(conversation, "headerHeight", "module.conversation.headerHeight") * scale,
      )
    : undefined;
  if (header) children.push(header);
  // The header surface bleeds behind Status Bar, but its layout box remains below it.
  if (status) children.push(status);

  const top = screen.y + (status?.box?.height ?? 0) + (header?.box?.height ?? 0);
  const closedBottom = screen.y + screen.height - navHeight;
  const composerBottom = textInputNode
    ? renderableVisualBounds(textInputNode).y
    : keyboardNode
      ? renderableVisualBounds(keyboardNode).y
      : closedBottom;
  const composerOpen = keyboardVisible || textInputVisible;
  const viewportMotion = conversation.messageViewportMotion
    ? requiredMotionContract(conversation, "messageViewportMotion", "module.conversation.messageViewportMotion")
    : {
        transition: "slide" as const,
        direction: "bottom" as const,
        bounds: "parent" as const,
        fade: false,
        translate: true,
        scale: false,
      };
  const motionProgress = motionFrameProgress(payload, viewportMotion, {
    trigger: optionalBoolean(preview, "composerTransitionTrigger"),
    timeSeconds: optionalNumber(preview, "composerTransitionTimeSeconds", 0),
  });
  const bottom = composerOpen
    ? lerp(closedBottom, composerBottom, motionProgress)
    : closedBottom;
  const messageViewport = {
    x: screen.x,
    y: top,
    width: screen.width,
    height: Math.max(0, bottom - top),
  };
  children.push({
    id: "module.conversation.messages",
    type: "group",
    frame: 0,
    box: messageViewport,
    style: {
      overflow: "hidden",
    },
    children: messageNodes(
      payload,
      componentBaseConfigs,
      conversation,
      preview,
      top,
      bottom,
      conversationFrame,
    ),
  });

  if (textInputNode) children.push(textInputNode);
  if (keyboardNode) children.push(keyboardNode);
  if (navigation) children.push(navigation);

  return {
    id: "module.conversation",
    type: "group",
    frame: 0,
    box: screen,
    style: {
      overflow: "hidden",
    },
    children,
  };
}

function runtimePreview(payload: DesignPreviewPayload): JsonRecord {
  const preview = parseObject(payload.designPreviewJson);
  if (payload.kind !== "moduleInstance") return preview;

  const instance = parseObject(payload.instanceJson);
  const content = asRecord(instance.content);
  const header = asRecord(content.header);
  const context = asRecord(instance.context);
  return {
    ...preview,
    actor: context.ownerActor,
    headerTitle: optionalString(header, "title"),
    headerSubtitle: optionalString(header, "subtitle"),
    messages: content.messages,
  };
}

function appWallpaperNode(
  payload: DesignPreviewPayload,
  screen: NonNullable<RenderableNode["box"]>,
): RenderableNode | undefined {
  const appConfig = parseObject(payload.appConfigJson ?? "{}");
  const wallpaper = asRecord(appConfig.wallpaper);
  const opacity = Math.max(0, Math.min(1, optionalNumber(wallpaper, "opacity", 1)));
  if (opacity <= 0) return undefined;

  const image = asRecord(wallpaper.image);
  const filePath = optionalString(image, "filePath");
  const kind = optionalString(wallpaper, "kind") || (filePath ? "image" : "solid");
  if (kind === "image") {
    const frame = mediaFrameUriForPath(payload, filePath, 0);
    if (frame.uri) {
      return {
        id: "module.conversation.wallpaper.image",
        type: "image",
        frame: 0,
        box: screen,
        asset: {
          type: "image",
          uri: frame.uri,
        },
        style: {
          objectFit: "cover",
          opacity,
        },
      };
    }
  }

  const modes = asRecord(appConfig.modes);
  const mode = asRecord(modes[payload.themeMode || "light"]);
  const modeWallpaper = asRecord(mode.wallpaper);
  const colorToken = optionalString(modeWallpaper, "color");
  if (!colorToken) return undefined;
  return {
    id: "module.conversation.wallpaper.color",
    type: "surface",
    frame: 0,
    box: screen,
    style: {
      background: selectedPaletteColor(payload, colorToken, opacity),
    },
  };
}

function messageNodes(
  payload: DesignPreviewPayload,
  componentBaseConfigs: JsonRecord,
  conversation: JsonRecord,
  preview: JsonRecord,
  top: number,
  bottom: number,
  conversationFrame: number,
) {
  const gap = numberToken(payload, optionalString(conversation, "messageGap") || "theme.spacing.m")
    * renderScale(payload);
  const gutter = spacingPair(payload, optionalString(conversation, "screenGutter") || "theme.spacing.l|theme.spacing.l");
  const bubbleVariant = requiredString(
    conversation,
    "bubbleVariant",
    "module.conversation.bubbleVariant",
  );
  const messages = visibleMessages(
    instanceMessages(preview),
    conversationFrame,
    optionalString(preview, "bubbleRevealMode") || "duringWriteOn",
  );
  const bubbleNode = (message: ConversationPreviewMessage, writeOnTrigger: boolean) => childRenderable(
    payload,
    componentBaseConfigs,
    "bubble",
    bubbleVariant,
    {
      state: message.state,
      sampleText: message.text,
      actor: preview.actor,
      mediaType: "none",
      maxWidth: optionalNumber(conversation, "bubbleMaxWidth", 66),
      writeOnTrigger,
      writeOnFrame: message.writeOnFrame,
      writeOnDurationFrames: message.writeOnDurationFrames,
      statusState: message.statusVisible ? message.statusState : "none",
      statusText: message.statusText,
    },
    (childPayload) => bubbleComponentToRenderable(childPayload, resolveBubbleComponent(childPayload)),
  );
  const entries = messages.map((message) => {
    const node = bubbleNode(message, message.writeOnTrigger);
    const bounds = renderableVisualBounds(node);
    const finalBounds = message.state === "outgoing" && message.writeOnTrigger
      ? renderableVisualBounds(bubbleNode(message, false))
      : bounds;
    return { node, bounds, finalBounds };
  });
  const totalHeight = entries.reduce((sum, entry) => sum + entry.finalBounds.height, 0)
    + Math.max(0, entries.length - 1) * gap;
  let y = bottom - gutter.y - totalHeight;
  return entries.map((entry, index) => {
    const { node, bounds, finalBounds } = entry;
    const message = messages[index]!;
    const offsetX = message.state === "outgoing"
      ? payload.previewFrame.screenX + payload.previewFrame.screenWidth - gutter.x - (finalBounds.x + finalBounds.width)
      : message.state === "system"
        ? payload.previewFrame.screenX + payload.previewFrame.screenWidth / 2 - (bounds.x + bounds.width / 2)
        : payload.previewFrame.screenX + gutter.x - bounds.x;
    const translated = translateRenderableNode(node, { x: offsetX, y: y - bounds.y });
    y += finalBounds.height + gap;
    return translated;
  });
}

type ConversationPreviewMessage = {
  state: string;
  text: string;
  statusState: string;
  statusText: string;
  delayAfterPreviousFrames: number;
  writeOnDurationFrames: number;
  writeOnTrigger: boolean;
  writeOnFrame: number;
  bubbleRevealMode: string;
  textInputVisible: boolean;
  keyboardVisible: boolean;
  statusVisible: boolean;
};

function instanceMessages(preview: JsonRecord): ConversationPreviewMessage[] {
  const messages = Array.isArray(preview.messages)
    ? preview.messages.map(asRecord)
    : Array.isArray(preview.instanceMessages)
      ? preview.instanceMessages.map(asRecord)
    : [];
  if (messages.length > 0) {
    return messages.map((message) => {
      const status = asRecord(message.status);
      const textReveal = asRecord(message.textReveal);
      return {
        state: optionalString(message, "direction") || "incoming",
        text: optionalString(message, "text"),
        statusState: optionalString(message, "statusState") || optionalString(status, "deliveryStatus") || "none",
        statusText: optionalString(message, "statusText") || optionalString(status, "text"),
        delayAfterPreviousFrames: Math.max(0, Math.floor(optionalNumber(message, "delayAfterPreviousFrames", 0))),
        writeOnDurationFrames: Math.max(0, Math.floor(optionalNumber(message, "writeOnDurationFrames", optionalNumber(textReveal, "durationFrames", 0)))),
        writeOnTrigger: false,
        writeOnFrame: 0,
        bubbleRevealMode: optionalString(message, "bubbleRevealMode") || "duringWriteOn",
        textInputVisible: optionalBoolean(message, "textInputVisible"),
        keyboardVisible: optionalBoolean(message, "keyboardVisible"),
        statusVisible: optionalBoolean(message, "statusVisible") || optionalString(message, "statusState") !== "none",
      };
    });
  }

  return [
    {
      state: "incoming",
      text: optionalString(preview, "message1Text"),
      statusState: "none",
      statusText: "",
      delayAfterPreviousFrames: 0,
      writeOnDurationFrames: 30,
      writeOnTrigger: false,
      writeOnFrame: 0,
      bubbleRevealMode: optionalString(preview, "bubbleRevealMode") || "duringWriteOn",
      textInputVisible: booleanPreviewValue(preview, "textInputVisible", false),
      keyboardVisible: booleanPreviewValue(preview, "keyboardVisible", false),
      statusVisible: false,
    },
    {
      state: "outgoing",
      text: optionalString(preview, "message2Text"),
      statusState: optionalString(preview, "message2StatusState") || "read",
      statusText: optionalString(preview, "message2StatusText"),
      delayAfterPreviousFrames: 0,
      writeOnDurationFrames: 30,
      writeOnTrigger: false,
      writeOnFrame: 0,
      bubbleRevealMode: optionalString(preview, "bubbleRevealMode") || "duringWriteOn",
      textInputVisible: booleanPreviewValue(preview, "textInputVisible", false),
      keyboardVisible: booleanPreviewValue(preview, "keyboardVisible", false),
      statusVisible: optionalString(preview, "message2StatusState") !== "none",
    },
    {
      state: "system",
      text: optionalString(preview, "message3Text"),
      statusState: "none",
      statusText: "",
      delayAfterPreviousFrames: 0,
      writeOnDurationFrames: 30,
      writeOnTrigger: false,
      writeOnFrame: 0,
      bubbleRevealMode: optionalString(preview, "bubbleRevealMode") || "duringWriteOn",
      textInputVisible: false,
      keyboardVisible: false,
      statusVisible: false,
    },
  ];
}

function visibleMessages(
  messages: ConversationPreviewMessage[],
  frame: number,
  fallbackRevealMode: string,
) {
  let cursor = 0;
  return messages.flatMap((message) => {
    const startFrame = cursor + message.delayAfterPreviousFrames;
    const isSystemMessage = message.state === "system";
    const effectiveWriteOnFrames = isSystemMessage ? 0 : message.writeOnDurationFrames;
    const revealEndFrame = startFrame + effectiveWriteOnFrames;
    cursor = revealEndFrame;
    const revealAfterWriteOn = !isSystemMessage
      && (message.bubbleRevealMode || fallbackRevealMode) === "afterWriteOn";
    const visibleAt = revealAfterWriteOn ? revealEndFrame : startFrame;
    if (frame < visibleAt) return [];
    return [{
      ...message,
      writeOnTrigger: !isSystemMessage && !revealAfterWriteOn && effectiveWriteOnFrames > 0,
      writeOnFrame: Math.max(0, frame - startFrame),
      writeOnDurationFrames: effectiveWriteOnFrames,
    }];
  });
}

function composerState(messages: ConversationPreviewMessage[], frame: number) {
  let cursor = 0;
  for (const message of messages) {
    const startFrame = cursor + message.delayAfterPreviousFrames;
    const effectiveWriteOnFrames = message.state === "system" ? 0 : message.writeOnDurationFrames;
    const endFrame = startFrame + effectiveWriteOnFrames;
    const writing = message.state === "outgoing"
      && effectiveWriteOnFrames > 0
      && frame >= startFrame
      && frame < endFrame;
    if (writing) {
      const textLength = Math.max(0, Math.min(
        Array.from(message.text).length,
        Math.floor(Array.from(message.text).length * (frame - startFrame) / Math.max(1, effectiveWriteOnFrames)),
      ));
      return {
        text: Array.from(message.text).slice(0, textLength).join(""),
        textInputVisible: message.textInputVisible,
        keyboardVisible: message.keyboardVisible,
      };
    }
    cursor = endFrame;
  }
  return { text: "", textInputVisible: false, keyboardVisible: false };
}

function booleanPreviewValue(preview: JsonRecord, key: string, fallback: boolean) {
  return typeof preview[key] === "boolean" ? preview[key] : fallback;
}

function lerp(from: number, to: number, progress: number) {
  return from + (to - from) * Math.max(0, Math.min(1, progress));
}

function childRenderable(
  payload: DesignPreviewPayload,
  componentBaseConfigs: JsonRecord,
  componentType: string,
  presetReference: string,
  designPreviewPatch: JsonRecord,
  render: (payload: DesignPreviewPayload) => RenderableNode,
) {
  const config = componentPresetConfig(componentBaseConfigs, componentType, presetReference);
  return render({
    ...payload,
    kind: "componentClass",
    componentType,
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify(designPreviewPatch),
  });
}

function headerNode(
  payload: DesignPreviewPayload,
  componentBaseConfigs: JsonRecord,
  conversation: JsonRecord,
  preview: JsonRecord,
  offsetY: number,
  height: number,
): RenderableNode {
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const title = optionalString(preview, "headerTitle");
  const subtitle = optionalString(preview, "headerSubtitle");
  const titleHeight = subtitle ? height * 0.46 : height;
  const avatarSize = Math.max(0, height - 16 * scale);
  const avatar = avatarComponentToRenderableAt(
    payload,
    resolveAvatarComponentFromRecords(
      componentPresetConfig(
        componentBaseConfigs,
        "avatar",
        requiredString(
          conversation,
          "headerAvatarVariant",
          "module.conversation.headerAvatarVariant",
        ),
      ),
      {
        ...preview,
        sampleSubtext: "",
      },
      componentBaseConfigs,
      "module.conversation.header.avatar",
    ),
    {
      x: screen.x + 12 * scale,
      y: screen.y + offsetY + (height - avatarSize) / 2,
      width: avatarSize,
      height: avatarSize,
    },
  );
  const textLeft = screen.x + 24 * scale + avatarSize;
  const textWidth = Math.max(0, screen.width - (textLeft - screen.x) - 16 * scale);
  return {
    id: "module.conversation.header",
    type: "group",
    frame: 0,
    box: {
      x: screen.x,
      y: screen.y + offsetY,
      width: screen.width,
      height,
    },
    style: {
    },
    children: [
      {
        id: "module.conversation.header.bleed",
        type: "surface",
        frame: 0,
        box: {
          x: screen.x,
          y: screen.y,
          width: screen.width,
          height: offsetY + height,
        },
        style: {
          background: selectedColor(payload, "theme.colors.surface"),
        },
      },
      avatar,
      textNode(payload, `${title}`, textLeft, screen.y + offsetY + 10 * scale, textWidth, titleHeight, 19 * scale, 700),
      ...(subtitle
        ? [textNode(payload, subtitle, textLeft, screen.y + offsetY + 36 * scale, textWidth, height * 0.34, 12 * scale, 500, "theme.colors.textSecondary")]
        : []),
      {
        id: "module.conversation.header.separator",
        type: "surface",
        frame: 0,
        box: {
          x: screen.x,
          y: screen.y + offsetY + height - Math.max(1, scale),
          width: screen.width,
          height: Math.max(1, scale),
        },
        style: {
          background: selectedColor(payload, "theme.colors.divider"),
        },
      },
    ],
  };
}

function textNode(
  payload: DesignPreviewPayload,
  text: string,
  x: number,
  y: number,
  width: number,
  height: number,
  fontSize: number,
  fontWeight: number,
  colorToken = "theme.colors.textPrimary",
): RenderableNode {
  return {
    id: `module.conversation.header.text.${text}`,
    type: "text",
    frame: 0,
    text,
    box: { x, y, width, height },
    style: {
      alignItems: "center",
      color: selectedColor(payload, colorToken),
      display: "flex",
      fontSize,
      fontWeight,
      overflow: "hidden",
      whiteSpace: "nowrap",
    },
  };
}

function spacingPair(payload: DesignPreviewPayload, value: string) {
  const [xToken = "theme.spacing.l", yToken = xToken] = value.split("|");
  const scale = renderScale(payload);
  return {
    x: numberToken(payload, xToken) * scale,
    y: numberToken(payload, yToken) * scale,
  };
}
