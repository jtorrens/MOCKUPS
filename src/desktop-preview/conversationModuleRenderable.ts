import type { RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { componentVariantConfig, embeddedComponentConfig } from "./componentPreviewDefaults.js";
import { componentClassToRenderable } from "./componentRenderableBoundary.js";
import {
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredRecord,
  requiredString,
} from "./componentResolverCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { requiredObjectArray } from "./previewJsonHelpers.js";
import { iconRowComponentToRenderableAt, measureIconRowComponent } from "./iconRowComponentRenderable.js";
import {
  iconRowButtonRuntimeDefaults,
  resolveIconRowComponentFromRecords,
} from "./iconRowComponentResolver.js";
import {
  numberToken,
  previewScreenBox,
  renderableVisualBounds,
  renderScale,
  selectedColor,
  translateRenderableNode,
} from "./componentRenderableCommon.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";
import { resolveConversationModule } from "./conversationModuleResolver.js";
import type {
  ConversationMessageContract,
  ConversationTimingContract,
} from "./conversationModuleContract.js";

type JsonRecord = Record<string, unknown>;

export function conversationModuleToRenderable(payload: DesignPreviewPayload): RenderableNode {
  const config = parseObject(payload.configJson);
  const contract = resolveConversationModule(payload);
  const preview = contract.preview;
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const conversation = requiredRecord(config, "conversation", "module config");
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
      )
    : undefined;
  const { composer, timing } = contract;
  const keyboardVisible = composer.keyboardVisible;
  const textInputVisible = composer.textInputVisible;
  const keyboard = keyboardVisible
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "keyboard",
        requiredString(conversation, "keyboardVariant", "module.conversation.keyboardVariant"),
        {
          text: composer.text,
          currentCharacter: composer.currentCharacter,
          motionElapsedMs: contract.motionElapsedMs,
        },
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
          availableWidth: screen.width / scale,
        },
        contract.textInputConfig,
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
  const bottom = composerOpen
    ? lerp(closedBottom, composerBottom, contract.viewportMotionProgress)
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
      contract.visibleMessages,
      top,
      bottom,
      timing,
      contract.motionElapsedMs,
      contract.scrollMotionProgress,
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
  messages: ConversationMessageContract[],
  top: number,
  bottom: number,
  timing: ConversationTimingContract,
  motionElapsedMs: number,
  resolvedScrollProgress: number,
) {
  const gap = numberToken(payload, optionalString(conversation, "messageGap") || "theme.spacing.m")
    * renderScale(payload);
  const gutter = spacingPair(payload, optionalString(conversation, "screenGutter") || "theme.spacing.l|theme.spacing.l");
  const bubbleVariant = requiredString(
    conversation,
    "bubbleVariant",
    "module.conversation.bubbleVariant",
  );
  const bubbleNode = (message: ConversationMessageContract, writeOnTrigger: boolean) => childRenderable(
    payload,
    componentBaseConfigs,
    "bubble",
    bubbleVariant,
    {
      state: message.state,
      sampleText: message.text,
      actor: message.actor,
      actorIdentityVisible: message.actorIdentityVisible,
      mediaType: message.mediaType,
      mediaSource: message.mediaSource,
      viewportSize: message.viewportSize,
      mediaScale: message.mediaScale,
      mediaOffset: message.mediaOffset,
      isPlaying: message.isPlaying,
      currentTimeSeconds: message.playbackTimeSeconds,
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
  const scrollProgress = targetOverflow !== previousOverflow
    ? resolvedScrollProgress
    : 1;
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

function withZIndex(node: RenderableNode, zIndex: number): RenderableNode {
  return {
    ...node,
    style: {
      ...node.style,
      zIndex,
    },
  };
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
  resolvedConfig?: JsonRecord,
) {
  const config = resolvedConfig
    ?? componentVariantConfig(componentBaseConfigs, componentType, variantReference);
  return componentClassToRenderable({
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
  const leftSlot = requiredRecord(
    conversation,
    "headerLeftIconRowSlot",
    "module.conversation",
  );
  const rightSlot = requiredRecord(
    conversation,
    "headerRightIconRowSlot",
    "module.conversation",
  );
  const leftInputs = requiredRecord(
    conversation,
    "headerLeftIconRowInputs",
    "module.conversation",
  );
  const rightInputs = requiredRecord(
    conversation,
    "headerRightIconRowInputs",
    "module.conversation",
  );
  const leftRow = resolveIconRowComponentFromRecords(
    embeddedComponentConfig(
      componentBaseConfigs,
      leftSlot,
      "iconRow",
      "module.conversation.headerLeftIconRowSlot",
    ),
    {
      ...leftInputs,
      structuralItems: requiredObjectArray(
        leftInputs,
        "items",
        "module.conversation.headerLeftIconRowInputs",
      ),
      buttonInputs: iconRowButtonRuntimeDefaults(
        requiredObjectArray(
          leftInputs,
          "items",
          "module.conversation.headerLeftIconRowInputs",
        ),
      ),
    },
    componentBaseConfigs,
    "module.conversation.header.left",
  );
  const rightRow = resolveIconRowComponentFromRecords(
    embeddedComponentConfig(
      componentBaseConfigs,
      rightSlot,
      "iconRow",
      "module.conversation.headerRightIconRowSlot",
    ),
    {
      ...rightInputs,
      structuralItems: requiredObjectArray(
        rightInputs,
        "items",
        "module.conversation.headerRightIconRowInputs",
      ),
      buttonInputs: iconRowButtonRuntimeDefaults(
        requiredObjectArray(
          rightInputs,
          "items",
          "module.conversation.headerRightIconRowInputs",
        ),
      ),
    },
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
