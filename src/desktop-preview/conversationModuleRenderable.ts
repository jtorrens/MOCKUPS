import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { avatarComponentToRenderableAt } from "./avatarComponentRenderable.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { bubbleComponentToRenderable } from "./bubbleComponentRenderable.js";
import { resolveBubbleComponent } from "./bubbleComponentResolver.js";
import { componentVariantConfig, embeddedComponentConfig } from "./componentPreviewDefaults.js";
import {
  componentClassToRenderable,
  resolveComponentRenderable,
} from "./componentRenderableBoundary.js";
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
import { iconRowComponentToRenderableAt, measureIconRowComponent } from "./iconRowComponentRenderable.js";
import { resolveIconRowComponentFromRecords } from "./iconRowComponentResolver.js";
import {
  cssColorWithAlpha,
  numberToken,
  previewScreenBox,
  renderableVisualBounds,
  renderScale,
  selectedColor,
  translateRenderableNode,
} from "./componentRenderableCommon.js";
import { wallpaperRenderable } from "./wallpaperRenderable.js";
import { resolveConversationModule } from "./conversationModuleResolver.js";
import {
  authoringVariantPayload,
  renderAuthoringSlot,
} from "./previewAuthoringTarget.js";
import type {
  ConversationMessageContract,
  ConversationModuleContract,
  ConversationTimingContract,
} from "./conversationModuleContract.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";
import { wrapExitMotionFrame, wrapMotionFrame } from "./previewMotionHelpers.js";

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
    "module.core.chat.useAppWallpaper",
  )
    ? wallpaperRenderable(payload, screen)
    : undefined;
  if (wallpaper) children.push(wallpaper);

  const themeStatusBarVariantReference = payload.themeStatusBarVariantReference?.trim() ?? "";
  const themeNavigationBarVariantReference = payload.themeNavigationBarVariantReference?.trim() ?? "";
  const status = requiredBoolean(
    conversation,
    "showStatusBar",
    "module.core.chat.showStatusBar",
  ) && themeStatusBarVariantReference
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "status_bar",
        "component.status_bar",
        themeStatusBarVariantReference,
        {},
      )
    : undefined;
  const navigation = requiredBoolean(
    conversation,
    "showNavigationBar",
    "module.core.chat.showNavigationBar",
  ) && themeNavigationBarVariantReference
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "navigation_bar",
        "component.navigation_bar",
        themeNavigationBarVariantReference,
        {},
      )
    : undefined;
  const { composer, timing } = contract;
  const keyboardVisible = composer.keyboardVisible;
  const textInputVisible = composer.textInputVisible;
  const keyboardSlot = requiredRecord(
    conversation,
    "keyboardSlot",
    "module.core.chat.keyboardSlot",
  );
  const textInputBarSlot = requiredRecord(
    conversation,
    "textInputBarSlot",
    "module.core.chat.textInputBarSlot",
  );
  const keyboard = keyboardVisible
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "keyboard",
        "component.keyboard",
        requiredString(keyboardSlot, "variantReference", "module.core.chat.keyboardSlot"),
        {
          text: composer.text,
          currentCharacter: composer.currentCharacter,
          trigger: composer.currentCharacter > 0,
          motionElapsedMs: contract.motionElapsedMs,
        },
        embeddedComponentConfig(
          componentBaseConfigs,
          keyboardSlot,
          "keyboard",
          "module.core.chat.keyboardSlot",
        ),
      )
    : undefined;
  const textInput = textInputVisible
    ? childRenderable(
        payload,
        componentBaseConfigs,
        "textInputBar",
        "component.textInputBar",
        requiredString(textInputBarSlot, "variantReference", "module.core.chat.textInputBarSlot"),
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
    "module.core.chat.showHeader",
  )
    ? headerNode(
        payload,
        componentBaseConfigs,
        conversation,
        preview,
        (status?.box?.height ?? 0),
        requiredNumber(conversation, "headerHeight", "module.core.chat.headerHeight") * scale,
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
    id: "module.core.chat.messages",
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
      contract.messageReflow,
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
  messageReflow: ConversationModuleContract["messageReflow"],
) {
  const gap = numberToken(payload, optionalString(conversation, "messageGap") || "theme.spacing.m")
    * renderScale(payload);
  const gutter = spacingPair(payload, optionalString(conversation, "screenGutter") || "theme.spacing.l|theme.spacing.l");
  const screen = previewScreenBox(payload);
  const bubbleSlot = requiredRecord(
    conversation,
    "bubbleSlot",
    "module.core.chat.bubbleSlot",
  );
  const bubbleNode = (message: ConversationMessageContract, writeOnTrigger: boolean) =>
    resolveComponentRenderable(
      childPayload(
        payload,
        "bubble",
        "component.bubble",
        requiredString(bubbleSlot, "variantReference", "module.core.chat.bubbleSlot"),
        {
          state: message.state,
          sampleText: message.text,
          actorId: message.actor?.id ?? "",
          actorName: message.actor?.displayName ?? "",
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
          motionElapsedMs: message.fullScreenMotionElapsedMs,
          maxWidth: optionalNumber(conversation, "bubbleMaxWidth", 66),
          textSizeToken: message.isTypingIndicator ? timing.typingIndicatorSizeToken : undefined,
          textAnimationMode: message.isTypingIndicator ? timing.typingIndicatorAnimation : undefined,
          textAnimationElapsedMs: message.isTypingIndicator ? motionElapsedMs : undefined,
          typingIndicator: message.isTypingIndicator,
          writeOnTrigger,
          writeOnFrame: message.writeOnFrame,
          writeOnDurationFrames: message.writeOnDurationFrames,
          keepCursorAfterWrite: message.keepCursorAfterWrite,
          statusState: message.statusVisible ? message.statusState : "none",
          statusText: message.statusVisible ? message.statusText : "",
        },
        embeddedComponentConfig(
          componentBaseConfigs,
          bubbleSlot,
          "bubble",
          "module.core.chat.bubbleSlot",
        ),
      ),
      resolveBubbleComponent,
      bubbleComponentToRenderable,
    );
  const resolveEntries = (sourceMessages: ConversationMessageContract[]) => sourceMessages.map((message) => {
    const bubble = bubbleNode(message, message.writeOnTrigger);
    const node = bubble.renderable;
    const bounds = renderableVisualBounds(node);
    const finalBounds = message.state === "outgoing" && message.writeOnTrigger
      ? renderableVisualBounds(bubbleNode(message, false).renderable)
      : bounds;
    return { id: message.id, node, bounds, finalBounds, alignment: bubble.resolved.alignment };
  });
  const entries = resolveEntries(messages);
  const totalHeight = entries.reduce((sum, entry) => sum + entry.finalBounds.height, 0)
    + Math.max(0, entries.length - 1) * gap;
  const viewportHeight = Math.max(0, bottom - top);
  const viewportBox: RenderableBox = {
    x: screen.x,
    y: top,
    width: screen.width,
    height: viewportHeight,
  };
  const targetOverflow = Math.max(0, gap + totalHeight - viewportHeight);
  const previousEntries = messageReflow
    ? resolveEntries(messageReflow.fromMessages)
    : entries;
  const previousHeight = previousEntries.reduce((sum, entry) => sum + entry.finalBounds.height, 0)
    + Math.max(0, previousEntries.length - 1) * gap;
  const previousOverflow = Math.max(0, gap + previousHeight - viewportHeight);
  const scrollProgress = messageReflow?.progress ?? 1;
  const previousYById = new Map<string, number>();
  let previousY = top + gap - previousOverflow;
  previousEntries.forEach((entry) => {
    previousYById.set(entry.id, previousY);
    previousY += entry.finalBounds.height + gap;
  });
  return entries.map((entry, index) => {
    const { node, bounds, alignment } = entry;
    const message = messages[index]!;
    const offsetX = alignment === "right"
      ? screen.x + screen.width - gutter.x - (bounds.x + bounds.width)
      : alignment === "center"
        ? screen.x + screen.width / 2 - (bounds.x + bounds.width / 2)
        : screen.x + gutter.x - bounds.x;
    const targetY = top + gap - targetOverflow
      + entries.slice(0, index).reduce((sum, current) => sum + current.finalBounds.height + gap, 0);
    const priorY = previousYById.get(message.id);
    const resolvedY = priorY === undefined
      ? targetY
      : lerp(priorY, targetY, scrollProgress);
    const translated = translateRenderableNode(node, { x: offsetX, y: resolvedY - bounds.y });
    if (!translated.box || !message.presenceMotionFrame || !message.presenceMotionKind) {
      return translated;
    }
    return message.presenceMotionKind === "enter"
      ? wrapMotionFrame(
          payload,
          translated,
          message.presenceMotion,
          message.presenceMotionFrame,
          translated.box,
          viewportBox,
        )
      : wrapExitMotionFrame(
          payload,
          translated,
          message.presenceMotion,
          message.presenceMotionFrame,
          translated.box,
          viewportBox,
        );
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
  childRecordClassId: string,
  variantReference: string,
  designPreviewPatch: JsonRecord,
  resolvedConfig?: JsonRecord,
) {
  return componentClassToRenderable(childPayload(
    payload,
    componentType,
    childRecordClassId,
    variantReference,
    designPreviewPatch,
    resolvedConfig
      ?? componentVariantConfig(componentBaseConfigs, componentType, variantReference),
  ));
}

function childPayload(
  payload: DesignPreviewPayload,
  componentType: string,
  childRecordClassId: string,
  variantReference: string,
  designPreviewPatch: JsonRecord,
  config: JsonRecord,
) {
  return authoringVariantPayload({
    ...payload,
    kind: "componentClass",
    componentType,
    configJson: JSON.stringify(config),
    designPreviewJson: JSON.stringify(designPreviewPatch),
  }, variantReference, childRecordClassId);
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
  const surfaceSlot = requiredRecord(
    conversation,
    "headerSurfaceSlot",
    "module.conversation",
  );
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
      "module.core.chat.headerLeftIconRowSlot",
    ),
    leftInputs,
    componentBaseConfigs,
    "module.core.chat.header.left",
  );
  const rightRow = resolveIconRowComponentFromRecords(
    embeddedComponentConfig(
      componentBaseConfigs,
      rightSlot,
      "iconRow",
      "module.core.chat.headerRightIconRowSlot",
    ),
    rightInputs,
    componentBaseConfigs,
    "module.core.chat.header.right",
  );
  const leftSize = measureIconRowComponent(payload, leftRow);
  const rightSize = measureIconRowComponent(payload, rightRow);
  const edgePadding = 12 * scale;
  const rowGap = 8 * scale;
  const centerLeft = screen.x + edgePadding + leftSize.width + (leftSize.width > 0 ? rowGap : 0);
  const centerRight = screen.x + screen.width - edgePadding - rightSize.width - (rightSize.width > 0 ? rowGap : 0);
  const avatarAlignment = optionalString(conversation, "headerAvatarAlignment") || "left";
  const avatarSlot = requiredRecord(
    conversation,
    "headerAvatarSlot",
    "module.core.chat.headerAvatarSlot",
  );
  const resolvedAvatar = resolveAvatarComponentFromRecords(
      embeddedComponentConfig(
        componentBaseConfigs,
        avatarSlot,
        "avatar",
        "module.core.chat.headerAvatarSlot",
      ),
      {
        ...preview,
        sampleSubtext: subtitle,
        showBadge: false,
        badgeIconToken: "system_check",
        badgeText: "1",
      },
      componentBaseConfigs,
      "module.core.chat.header.avatar",
    );
  const resolvedSurface = resolveSurfaceComponentAtSize(
    embeddedComponentConfig(
      componentBaseConfigs,
      surfaceSlot,
      "surface",
      "module.core.chat.headerSurfaceSlot",
    ),
    {
      width: screen.width / scale,
      height: (offsetY + height) / scale,
    },
    "module.core.chat.header.surface",
  );
  const surfaceNode = renderAuthoringSlot(
    payload,
    "module.core.chat",
    "module.core.chat.headerSurface.editor",
    "component.surface",
    "component.surface.backgroundColorToken",
    (slotPayload) => surfaceComponentToRenderableAt(
      slotPayload,
      resolvedSurface,
      {
        x: screen.x,
        y: screen.y,
        width: screen.width,
        height: offsetY + height,
      },
      requiredBoolean(
        conversation,
        "headerUseActorColor",
        "module.core.chat.headerUseActorColor",
      )
        ? {
            background: cssColorWithAlpha(
              resolvedAvatar.actor.avatar.backgroundColor,
              resolvedSurface.backgroundAlpha,
            ),
          }
        : undefined,
    ),
  );
  const avatarSize = resolvedAvatar.size * scale;
  const unresolvedAvatar = renderAuthoringSlot(
    payload,
    "module.core.chat",
    "module.core.chat.headerAvatar.editor",
    "component.avatar",
    "component.avatar.defaultSize",
    (slotPayload) => avatarComponentToRenderableAt(
      slotPayload,
      resolvedAvatar,
      {
        x: 0,
        // Header content starts below Status Bar; only the background bleeds upward.
        y: screen.y + offsetY + (height - avatarSize) / 2,
        width: avatarSize,
        height: avatarSize,
      },
    ),
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
  const leftRowNode = renderAuthoringSlot(
    payload,
    "module.core.chat",
    "module.core.chat.headerLeftIconRow.editor",
    "component.iconRow",
    "component.iconRow.items",
    (slotPayload) => iconRowComponentToRenderableAt(slotPayload, leftRow, {
      x: screen.x + edgePadding,
      y: screen.y + offsetY + (height - leftSize.height) * 0.5,
      width: leftSize.width,
      height: leftSize.height,
    }),
  );
  const rightRowNode = renderAuthoringSlot(
    payload,
    "module.core.chat",
    "module.core.chat.headerRightIconRow.editor",
    "component.iconRow",
    "component.iconRow.items",
    (slotPayload) => iconRowComponentToRenderableAt(slotPayload, rightRow, {
      x: screen.x + screen.width - edgePadding - rightSize.width,
      y: screen.y + offsetY + (height - rightSize.height) * 0.5,
      width: rightSize.width,
      height: rightSize.height,
    }),
  );
  return {
    id: "module.core.chat.header",
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
      surfaceNode,
      avatar,
      leftRowNode,
      rightRowNode,
      ...(requiredBoolean(
        conversation,
        "showHeaderSeparator",
        "module.core.chat.showHeaderSeparator",
      ) ? [({
        id: "module.core.chat.header.separator",
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
      } satisfies RenderableNode)] : []),
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
