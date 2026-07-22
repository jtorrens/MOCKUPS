import type { RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { bubbleComponentToRenderable } from "./bubbleComponentRenderable.js";
import { resolveBubbleComponent } from "./bubbleComponentResolver.js";
import { componentVariantConfig, mergeComponentDefaults } from "./componentPreviewDefaults.js";
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
import { iconRowComponentToRenderableAt, measureIconRowComponent } from "./iconRowComponentRenderable.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
import { navigationBarComponentToRenderable } from "./navigationBarComponentRenderable.js";
import { resolveNavigationBarComponent } from "./navigationBarComponentResolver.js";
import {
  numberToken,
  previewScreenBox,
  renderableVisualBounds,
  renderScale,
  selectedColor,
  translateRenderableNode,
} from "./componentRenderableCommon.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";
import { statusBarComponentToRenderable } from "./statusBarComponentRenderable.js";
import { resolveStatusBarComponent } from "./statusBarComponentResolver.js";
import { textInputBarComponentToRenderable } from "./textInputBarComponentRenderable.js";
import { resolveTextInputBarComponent } from "./textInputBarComponentResolver.js";
import { motionFrameProgress, requiredMotionContract } from "./previewMotionHelpers.js";
import {
  simpleWriteOnFrameVisibleCount,
  textGraphemes,
} from "./previewTextRevealHelpers.js";
import { resolveConversationModuleFrame } from "./conversationModuleResolver.js";

type JsonRecord = Record<string, unknown>;

export function conversationMessageActorIdentityVisible(
  conversationType: string,
  direction: string,
) {
  return conversationType === "group" && direction === "incoming";
}

export function conversationModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const config = parseObject(payload.configJson);
  const preview = resolveConversationModuleFrame(payload);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const conversation = {
    ...asRecord(config.conversation),
  };
  const screen = previewScreenBox(payload);
  const scale = renderScale(payload);
  const children: RenderableNode[] = [];
  const wallpaper = requiredBoolean(
    conversation,
    "useAppWallpaper",
    "module.conversation.useAppWallpaper",
  )
    ? wallpaperRenderable(payload, screen)
    : undefined;
  if (wallpaper) children.push(wallpaper);

  const themeStatusBarVariantReference = payload.themeStatusBarVariantReference?.trim() ?? "";
  const themeNavigationBarVariantReference = payload.themeNavigationBarVariantReference?.trim() ?? "";
  const status = requiredBoolean(
    conversation,
    "showStatusBar",
    "module.conversation.showStatusBar",
  ) && themeStatusBarVariantReference
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "status_bar",
        themeStatusBarVariantReference,
        {},
        (childPayload) =>
          statusBarComponentToRenderable(childPayload, resolveStatusBarComponent(childPayload)),
      )
    : undefined;
  const navigation = requiredBoolean(
    conversation,
    "showNavigationBar",
    "module.conversation.showNavigationBar",
  ) && themeNavigationBarVariantReference
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "navigation_bar",
        themeNavigationBarVariantReference,
        {},
        (childPayload) =>
          navigationBarComponentToRenderable(
            childPayload,
            resolveNavigationBarComponent(childPayload),
          ),
      )
    : undefined;
  const conversationFrame = Math.max(0, Math.floor(optionalNumber(preview, "conversationFrame", Number.MAX_SAFE_INTEGER)));
  const motionElapsedMs = conversationFrame / Math.max(1, payload.frameRate) * 1000;
  const timing = conversationTiming(conversation, preview);
  const composer = composerState(conversationMessages(preview), conversationFrame, timing);
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
          currentCharacter: composer.currentCharacter,
          motionElapsedMs,
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
  const keyboardBaseTop = keyboardNode?.box?.y
    ?? screen.y + screen.height - navHeight;
  const textInputTargetY = keyboardBaseTop - (textInput?.box?.height ?? 0);
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
    ? textInputNode.box?.y ?? closedBottom
    : keyboardNode
      ? keyboardNode.box?.y ?? closedBottom
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
    elapsedMs: optionalNumber(preview, "composerTransitionElapsedMs", 0),
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
      motionElapsedMs,
      timing,
    ),
  });

  if (textInputNode) children.push(withZIndex(textInputNode, 10));
  if (keyboardNode) children.push(withZIndex(keyboardNode, 20));
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

function messageNodes(
  payload: DesignPreviewPayload,
  componentBaseConfigs: JsonRecord,
  conversation: JsonRecord,
  preview: JsonRecord,
  top: number,
  bottom: number,
  conversationFrame: number,
  motionElapsedMs: number,
  timing: ConversationTiming,
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
    conversationMessages(preview),
    conversationFrame,
    timing,
  );
  const conversationType = requiredString(
    preview,
    "conversationType",
    "module.conversation.input.conversationType",
  );
  if (conversationType !== "individual" && conversationType !== "group") {
    throw new Error(`Unsupported Conversation type ${conversationType}`);
  }
  const bubbleNode = (message: ConversationPreviewMessage, writeOnTrigger: boolean) => childRenderable(
    payload,
    componentBaseConfigs,
    "bubble",
    bubbleVariant,
    {
      state: message.state,
      sampleText: message.text,
      actor: message.actor,
      actorIdentityVisible: conversationMessageActorIdentityVisible(conversationType, message.state),
      mediaType: message.mediaType,
      mediaSource: message.mediaSource,
      viewportSize: message.viewportSize,
      mediaScale: message.mediaScale,
      mediaOffset: message.mediaOffset,
      isPlaying: message.isPlaying,
      currentTimeSeconds: messagePlaybackTimeSeconds(message, payload.frameRate),
      durationSeconds: message.durationSeconds,
      playbackMode: message.playbackMode,
      isFullScreen: message.isFullScreen,
      fullScreenTransition: message.fullScreenTransition,
      fullframeOrientation: message.fullframeOrientation,
      controlsElapsedMs: message.controlsElapsedMs,
      motionElapsedMs,
      maxWidth: optionalNumber(conversation, "bubbleMaxWidth", 66),
      textSizeToken: message.isTypingIndicator ? timing.typingIndicatorSizeToken : undefined,
      textAnimationMode: message.isTypingIndicator ? timing.typingIndicatorAnimation : undefined,
      textAnimationElapsedMs: message.isTypingIndicator ? motionElapsedMs : undefined,
      typingIndicator: message.isTypingIndicator,
      writeOnTrigger,
      writeOnFrame: message.writeOnFrame,
      writeOnDurationFrames: message.writeOnDurationFrames,
      statusState: message.statusVisible ? message.statusState : "none",
      statusText: message.statusVisible ? message.statusText : "",
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
  const viewportHeight = Math.max(0, bottom - top);
  const targetOverflow = Math.max(0, gap + totalHeight - viewportHeight);
  const latestAppearanceFrame = messages.reduce(
    (latest, message) => Math.max(latest, message.visibleAtFrame),
    0,
  );
  const previousEntries = entries.filter((_, index) =>
    messages[index]!.visibleAtFrame < latestAppearanceFrame);
  const previousHeight = previousEntries.reduce((sum, entry) => sum + entry.finalBounds.height, 0)
    + Math.max(0, previousEntries.length - 1) * gap;
  const previousOverflow = Math.max(0, gap + previousHeight - viewportHeight);
  const scrollProgress = motionFrameProgress(
    payload,
    {
      transition: "slide",
      direction: "bottom",
      bounds: "parent",
      fade: false,
      translate: true,
      scale: false,
    },
    {
      trigger: targetOverflow !== previousOverflow,
      elapsedMs: Math.max(0, conversationFrame - latestAppearanceFrame)
        / Math.max(1, payload.frameRate) * 1000,
    },
  );
  const scrollOffset = lerp(previousOverflow, targetOverflow, scrollProgress);
  let y = top + gap - scrollOffset;
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
  actor: JsonRecord;
  state: string;
  text: string;
  statusState: string;
  statusText: string;
  delayAfterPreviousFrames: number;
  writeOnDurationFrames: number;
  timelineBodyDurationFrames: number;
  timelineStartFrame: number;
  timelineEndFrame: number;
  timelineRevealAtFrame: number;
  postWriteOnHoldFrames: number;
  writeOnTrigger: boolean;
  writeOnFrame: number;
  statusVisible: boolean;
  visibleAtFrame: number;
  mediaType: "none" | "image" | "video" | "audio";
  mediaSource: string;
  viewportSize: string;
  mediaScale: number;
  mediaOffset: string;
  isPlaying: boolean;
  currentTimeSeconds: number;
  durationSeconds: number;
  playbackMode: "once" | "loop";
  playDurationFrames: number;
  playbackFrame: number;
  isFullScreen: boolean;
  fullScreenTransition: boolean;
  fullframeOrientation: string;
  controlsElapsedMs: number;
  isTypingIndicator: boolean;
};

type IncomingRevealMode = "instant" | "writeOn" | "typingIndicator";
type TypingIndicatorAnimation = "none" | "pulsating" | "wave";

type ConversationTiming = {
  bubbleRevealMode: "duringWriteOn" | "afterWriteOn";
  incomingRevealMode: IncomingRevealMode;
  textInputVisible: boolean;
  keyboardVisible: boolean;
  typingIndicatorText: string;
  typingIndicatorSizeToken: string;
  typingIndicatorAnimation: TypingIndicatorAnimation;
};

function conversationTiming(conversation: JsonRecord, preview: JsonRecord): ConversationTiming {
  const incomingRevealMode = optionalString(preview, "incomingRevealMode")
    || optionalString(conversation, "incomingRevealMode");
  const bubbleRevealMode = optionalString(preview, "bubbleRevealMode")
    || optionalString(conversation, "bubbleRevealMode");
  return {
    bubbleRevealMode: bubbleRevealMode === "afterWriteOn" ? "afterWriteOn" : "duringWriteOn",
    incomingRevealMode: incomingRevealMode === "writeOn" || incomingRevealMode === "typingIndicator"
      ? incomingRevealMode
      : "instant",
    textInputVisible: optionalBooleanWithFallback(preview, conversation, "textInputVisible", true),
    keyboardVisible: optionalBooleanWithFallback(preview, conversation, "keyboardVisible", true),
    typingIndicatorText: optionalString(preview, "typingIndicatorText")
      || optionalString(conversation, "typingIndicatorText")
      || "•••",
    typingIndicatorSizeToken: optionalString(preview, "typingIndicatorSizeToken")
      || optionalString(conversation, "typingIndicatorSizeToken")
      || "theme.typography.sizes.m",
    typingIndicatorAnimation: typingIndicatorAnimation(
      optionalString(preview, "typingIndicatorAnimation")
        || optionalString(conversation, "typingIndicatorAnimation"),
    ),
  };
}

function typingIndicatorAnimation(value: string | undefined): TypingIndicatorAnimation {
  return value === "none" || value === "wave" ? value : "pulsating";
}

function optionalBooleanWithFallback(
  primary: JsonRecord,
  secondary: JsonRecord,
  key: string,
  fallback: boolean,
) {
  if (typeof primary[key] === "boolean") return primary[key];
  if (typeof secondary[key] === "boolean") return secondary[key];
  return fallback;
}

function conversationMessages(preview: JsonRecord): ConversationPreviewMessage[] {
  const messages = Array.isArray(preview.messages)
    ? preview.messages.map(asRecord)
    : [];
  if (messages.length > 0) {
    return messages.map((message) => {
      const status = asRecord(message.status);
      return {
        actor: asRecord(message.actor),
        state: optionalString(message, "direction") || "incoming",
        text: optionalString(message, "text"),
        statusState: optionalString(message, "statusState") || optionalString(status, "deliveryStatus") || "none",
        statusText: optionalString(message, "statusText") || optionalString(status, "text"),
        delayAfterPreviousFrames: Math.max(0, Math.floor(optionalNumber(message, "delayAfterPreviousFrames", 0))),
        writeOnDurationFrames: Math.max(0, Math.floor(optionalNumber(message, "writeOnDurationFrames", 0))),
        timelineBodyDurationFrames: Math.max(0, Math.floor(optionalNumber(message, "timelineBodyDurationFrames", 0))),
        timelineStartFrame: Math.max(0, Math.floor(optionalNumber(message, "timelineStartFrame", 0))),
        timelineEndFrame: Math.max(0, Math.floor(optionalNumber(message, "timelineEndFrame", 0))),
        timelineRevealAtFrame: Math.max(0, Math.floor(optionalNumber(message, "timelineRevealAtFrame", 0))),
        postWriteOnHoldFrames: Math.max(0, Math.floor(optionalNumber(message, "postWriteOnHoldFrames", 0))),
        writeOnTrigger: false,
        writeOnFrame: Math.max(0, Math.floor(optionalNumber(message, "writeOnFrame", 0))),
        statusVisible: optionalBoolean(message, "statusVisible") || optionalString(message, "statusState") !== "none",
        visibleAtFrame: 0,
        mediaType: messageMediaType(message),
        mediaSource: optionalString(message, "mediaSource"),
        viewportSize: optionalString(message, "viewportSize") || "240|160",
        mediaScale: optionalNumber(message, "mediaScale", 1),
        mediaOffset: optionalString(message, "mediaOffset") || "0|0",
        isPlaying: optionalBoolean(message, "isPlaying"),
        currentTimeSeconds: optionalNumber(message, "currentTimeSeconds", 0),
        durationSeconds: Math.max(1, optionalNumber(message, "durationSeconds", 12)),
        playbackMode: playbackMode(optionalString(message, "playbackMode")),
        playDurationFrames: Math.max(1, Math.floor(optionalNumber(message, "playDurationFrames", 72))),
        playbackFrame: Math.max(0, Math.floor(optionalNumber(message, "playbackFrame", 0))),
        isFullScreen: optionalBoolean(message, "isFullScreen"),
        fullScreenTransition: optionalBoolean(message, "fullScreenTransition"),
        fullframeOrientation: optionalString(message, "fullframeOrientation") || "portrait",
        controlsElapsedMs: optionalNumber(message, "controlsElapsedMs", 0),
        isTypingIndicator: false,
      };
    });
  }

  return [];
}

function visibleMessages(
  messages: ConversationPreviewMessage[],
  frame: number,
  timing: ConversationTiming,
) {
  return messages.flatMap((message) => {
    const startFrame = message.timelineStartFrame;
    const isSystemMessage = message.state === "system";
    const isOutgoingMessage = message.state === "outgoing";
    const isIncomingMessage = message.state === "incoming";
    const effectiveWriteOnFrames = isSystemMessage ? 0 : message.writeOnDurationFrames;
    const holdFrames = isOutgoingMessage ? message.postWriteOnHoldFrames : 0;
    const revealEndFrame = startFrame + effectiveWriteOnFrames;
    const revealAfterWriteOn = isOutgoingMessage && timing.bubbleRevealMode === "afterWriteOn";
    const visibleAt = revealAfterWriteOn ? message.timelineRevealAtFrame : startFrame;
    if (frame < visibleAt) return [];
    const incomingTyping = isIncomingMessage
      && timing.incomingRevealMode === "typingIndicator"
      && frame < revealEndFrame;
    const incomingWriteOn = isIncomingMessage
      && timing.incomingRevealMode === "writeOn"
      && effectiveWriteOnFrames > 0
      && frame < revealEndFrame;
    const messageIsWriting = frame < revealEndFrame
      && effectiveWriteOnFrames > 0
      && (isOutgoingMessage || incomingWriteOn || incomingTyping);
    return [{
      ...message,
      visibleAtFrame: visibleAt,
      text: incomingTyping ? timing.typingIndicatorText : message.text,
      mediaType: messageIsWriting ? "none" as const : message.mediaType,
      mediaSource: messageIsWriting ? "" : message.mediaSource,
      isTypingIndicator: incomingTyping,
      writeOnTrigger: (isOutgoingMessage || incomingWriteOn)
        && !revealAfterWriteOn
        && effectiveWriteOnFrames > 0,
      writeOnFrame: message.writeOnFrame,
      writeOnDurationFrames: effectiveWriteOnFrames,
    }];
  });
}

function composerState(
  messages: ConversationPreviewMessage[],
  frame: number,
  timing: ConversationTiming,
) {
  for (const message of messages) {
    const startFrame = message.timelineStartFrame;
    const effectiveWriteOnFrames = message.state === "system" ? 0 : message.writeOnDurationFrames;
    const endFrame = startFrame + effectiveWriteOnFrames;
    const holdEndFrame = message.timelineRevealAtFrame;
    const composerVisible = message.state === "outgoing"
      && effectiveWriteOnFrames > 0
      && frame >= startFrame
      && frame < holdEndFrame;
    if (composerVisible) {
      const graphemes = textGraphemes(message.text);
      const writeOnInProgress = frame < endFrame;
      const textLength = writeOnInProgress
        ? simpleWriteOnFrameVisibleCount(message.text, {
            enabled: true,
            frame: message.writeOnFrame,
            durationFrames: effectiveWriteOnFrames,
          })
        : graphemes.length;
      return {
        text: graphemes.slice(0, textLength).join(""),
        // The composer remains visible during the post-write-on hold, but the
        // physical key is only down while a grapheme is actively being typed.
        currentCharacter: writeOnInProgress ? textLength : 0,
        textInputVisible: timing.textInputVisible,
        keyboardVisible: timing.keyboardVisible,
      };
    }
  }
  return { text: "", currentCharacter: 0, textInputVisible: false, keyboardVisible: false };
}

function withZIndex(node: RenderableNode, zIndex: number): RenderableNode {
  return {
    ...node,
    style: {
      ...node.style,
      zIndex,
    },
  };
}

function messageMediaType(message: JsonRecord): ConversationPreviewMessage["mediaType"] {
  const mediaType = optionalString(message, "mediaType");
  return mediaType === "image" || mediaType === "video" || mediaType === "audio"
    ? mediaType
    : "none";
}

function playbackMode(value: string): ConversationPreviewMessage["playbackMode"] {
  return value === "loop" ? "loop" : "once";
}

function messagePlaybackTimeSeconds(message: ConversationPreviewMessage, frameRate: number) {
  const elapsedSeconds = message.playbackFrame > 0
    ? message.playbackFrame / Math.max(1, frameRate)
    : message.currentTimeSeconds;
  if (message.playbackMode === "loop") {
    return elapsedSeconds % message.durationSeconds;
  }
  return Math.min(message.durationSeconds, Math.max(0, elapsedSeconds));
}

function lerp(from: number, to: number, progress: number) {
  return from + (to - from) * Math.max(0, Math.min(1, progress));
}

function childRenderable(
  payload: DesignPreviewPayload,
  componentBaseConfigs: JsonRecord,
  componentType: string,
  variantReference: string,
  designPreviewPatch: JsonRecord,
  render: (payload: DesignPreviewPayload) => RenderableNode,
) {
  const config = componentVariantConfig(componentBaseConfigs, componentType, variantReference);
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
  const subtitle = optionalString(preview, "headerSubtitle");
  const leftSlot = asRecord(conversation.headerLeftIconRowSlot);
  const rightSlot = asRecord(conversation.headerRightIconRowSlot);
  const leftInputs = asRecord(conversation.headerLeftIconRowInputs);
  const rightInputs = asRecord(conversation.headerRightIconRowInputs);
  const leftRow = resolveIconRowComponentFromRecords(
    mergeComponentDefaults(
      componentVariantConfig(componentBaseConfigs, "iconRow", requiredString(leftSlot, "variantReference", "module.conversation.headerLeftIconRowSlot.variantReference")),
      asRecord(leftSlot.overrides),
    ),
    leftInputs,
    componentBaseConfigs,
    "module.conversation.header.left",
  );
  const rightRow = resolveIconRowComponentFromRecords(
    mergeComponentDefaults(
      componentVariantConfig(componentBaseConfigs, "iconRow", requiredString(rightSlot, "variantReference", "module.conversation.headerRightIconRowSlot.variantReference")),
      asRecord(rightSlot.overrides),
    ),
    rightInputs,
    componentBaseConfigs,
    "module.conversation.header.right",
  );
  const leftSize = measureIconRowComponent(payload, leftRow);
  const rightSize = measureIconRowComponent(payload, rightRow);
  const edgePadding = 12 * scale;
  const rowGap = 8 * scale;
  const centerLeft = screen.x + edgePadding + leftSize.width + (leftSize.width > 0 ? rowGap : 0);
  const centerRight = screen.x + screen.width - edgePadding - rightSize.width - (rightSize.width > 0 ? rowGap : 0);
  const avatarAlignment = optionalString(conversation, "headerAvatarAlignment") || "left";
  const resolvedAvatar = resolveAvatarComponentFromRecords(
      componentVariantConfig(
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
        sampleSubtext: subtitle,
        showBadge: false,
        badgeIconToken: "system_check",
        badgeText: "1",
      },
      componentBaseConfigs,
      "module.conversation.header.avatar",
    );
  const avatarSize = resolvedAvatar.size * scale;
  const unresolvedAvatar = avatarComponentToRenderableAt(
    payload,
    resolvedAvatar,
    {
      x: 0,
      // Header content starts below Status Bar; only the background bleeds upward.
      y: screen.y + offsetY + (height - avatarSize) / 2,
      width: avatarSize,
      height: avatarSize,
    },
  );
  const avatarVisualWidth = unresolvedAvatar.box?.width ?? avatarSize;
  const avatarTargetX = avatarAlignment === "right"
    ? centerRight - avatarVisualWidth
    : avatarAlignment === "center"
      ? centerLeft + Math.max(0, centerRight - centerLeft - avatarVisualWidth) * 0.5
      : centerLeft;
  const avatar = translateRenderableNode(unresolvedAvatar, {
    x: avatarTargetX - (unresolvedAvatar.box?.x ?? 0),
    y: 0,
  });
  const leftRowNode = iconRowComponentToRenderableAt(payload, leftRow, {
    x: screen.x + edgePadding,
    y: screen.y + offsetY + (height - leftSize.height) * 0.5,
    width: leftSize.width,
    height: leftSize.height,
  });
  const rightRowNode = iconRowComponentToRenderableAt(payload, rightRow, {
    x: screen.x + screen.width - edgePadding - rightSize.width,
    y: screen.y + offsetY + (height - rightSize.height) * 0.5,
    width: rightSize.width,
    height: rightSize.height,
  });
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
      leftRowNode,
      rightRowNode,
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

function spacingPair(payload: DesignPreviewPayload, value: string) {
  const [xToken = "theme.spacing.l", yToken = xToken] = value.split("|");
  const scale = renderScale(payload);
  return {
    x: numberToken(payload, xToken) * scale,
    y: numberToken(payload, yToken) * scale,
  };
}
