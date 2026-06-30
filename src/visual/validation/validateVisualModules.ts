import shotExample from "../../../docs/examples/shot_chat.json" with {
  type: "json",
};
import {
  parseKeyboardRows,
  STANDARD_IOS_KEYBOARD_LAYOUT,
} from "../../domain/keyboards/standardKeyboardLayout.js";
import { loadExampleRepository } from "../../domain/repository/fixtureLoader.js";
import { resolveShot } from "../../domain/resolvers/index.js";
import { ResolvedChatScreenPropsSchema } from "../../domain/schemas/index.js";
import { getVisualModule, visualModuleRegistry } from "../modules/registry.js";
import { RenderableNodeSchema } from "../renderable/schema.js";

function assert(condition: unknown, message: string): asserts condition {
  if (!condition) {
    throw new Error(message);
  }
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function isBox(
  value: unknown,
): value is { x: number; y: number; width: number; height: number } {
  return (
    isRecord(value) &&
    typeof value.x === "number" &&
    typeof value.y === "number" &&
    typeof value.width === "number" &&
    typeof value.height === "number"
  );
}

function collectNodes(
  node: ReturnType<typeof RenderableNodeSchema.parse>,
): ReturnType<typeof RenderableNodeSchema.parse>[] {
  return [node, ...(node.children ?? []).flatMap(collectNodes)];
}

const repository = loadExampleRepository();
const resolvedShot = resolveShot({
  repository,
  productionId: shotExample.production_id,
  shotId: shotExample.shot.id,
  shotFrame: 60,
});
const chatInstance = resolvedShot.active_screen_instances.find(
  (screen) => screen.screen_type === "chat",
);
assert(chatInstance?.resolved_props, "Chat props must resolve at shot frame 60");

const chatProps = ResolvedChatScreenPropsSchema.parse(
  chatInstance.resolved_props,
);
const chatModule = getVisualModule("chat_screen");
const tree = RenderableNodeSchema.parse(chatModule.render(chatProps));
const secondTree = RenderableNodeSchema.parse(chatModule.render(chatProps));

assert(tree.type === "chat_screen", "Root node must be chat_screen");
const childTypes = tree.children?.map((child) => child.type) ?? [];
assert(childTypes[0] === "wallpaper", "Wallpaper must render as the back layer");
assert(childTypes.includes("status_bar"), "Tree must contain status_bar");
assert(childTypes.includes("navigation_bar"), "Tree must contain navigation_bar");
assert(childTypes.includes("chat_header"), "Tree must contain chat_header");
assert(
  childTypes.includes("message_list"),
  "Tree must contain a clipped message_list",
);
assert(
  collectNodes(tree).filter((node) => node.type === "message_bubble").length ===
    chatProps.messages.length,
  "Tree must contain one message bubble per resolved message",
);
const headerNode = tree.children?.find((child) => child.type === "chat_header");
const statusNode = tree.children?.find((child) => child.type === "status_bar");
const navigationNode = tree.children?.find(
  (child) => child.type === "navigation_bar",
);
const wallpaperNode = tree.children?.find((child) => child.type === "wallpaper");
const bubbleNodes = collectNodes(tree).filter(
  (child) => child.type === "message_bubble",
);
const receivedBubble = bubbleNodes.find((child) => child.role === "incoming");
const sentBubble = bubbleNodes.find((child) => child.role === "outgoing");
const layoutMetadata = tree.metadata?.layout;
assert(headerNode?.box?.height === 288, "Header must use the scaled height token");
assert(tree.box, "ChatScreen root must have a box");
assert(
  wallpaperNode?.box?.width === tree.box.width &&
    wallpaperNode.box.height === tree.box.height,
  "Wallpaper must cover the full chat screen",
);
assert(
  wallpaperNode.transform?.opacity === 1,
  "Wallpaper must receive app-level opacity",
);
assert(statusNode?.box, "StatusBar must have a box");
assert(navigationNode?.box, "NavigationBar must have a box");
assert(headerNode?.box, "ChatHeader must have a box");
assert(
  isRecord(layoutMetadata) &&
    isBox(layoutMetadata.messageListBox) &&
    isBox(layoutMetadata.messageAreaBox),
  "Chat layout must expose the message-list bounds",
);
const messageListBox = layoutMetadata.messageListBox;
const messageAreaBox = layoutMetadata.messageAreaBox;
assert(
  messageListBox.x === 0 && messageListBox.width === tree.box.width,
  "Message list clip must span the full screen width",
);
assert(
  messageAreaBox.x === 72 &&
    messageAreaBox.x + messageAreaBox.width === 1218,
  "Message area must use the scaled screen gutter",
);
assert(
  receivedBubble?.style?.tailStyle === "rounded_wedge",
  "Message bubble must use the resolved tail style token",
);
assert(
  receivedBubble?.style?.tailVerticalPosition === "bottom" &&
    receivedBubble.style.tailScale === 1,
  "Message bubble must expose resolved tail position and scale tokens",
);
assert(
  receivedBubble &&
    collectNodes(receivedBubble).some((child) => child.type === "message_bubble_shape") &&
    collectNodes(receivedBubble).some((child) => child.type === "message_bubble_tail"),
  "Message bubble with a tail must render a tail node",
);
assert(
  receivedBubble?.box && sentBubble?.box,
  "Received and sent bubbles must have boxes",
);
for (const bubble of bubbleNodes) {
  assert(bubble.box, `Bubble ${bubble.id} must have a box`);
  assert(
    bubble.box.x >= messageAreaBox.x &&
      bubble.box.x + bubble.box.width <=
        messageAreaBox.x + messageAreaBox.width,
    `Bubble ${bubble.id} must stay inside message-area horizontal bounds`,
  );
}
assert(
  receivedBubble.box.x < sentBubble.box.x,
  "Received bubbles must align left of sent bubbles",
);
assert(
  Math.min(
    receivedBubble.box.x,
    ...collectNodes(receivedBubble)
      .filter(
        (node) =>
          (node.type === "message_bubble_tail" || node.type === "avatar") &&
          node.box,
      )
      .map((node) => node.box!.x),
  ) === messageAreaBox.x,
  "Received avatar/bubble/tail unit must align to the scaled message-area left edge",
);
assert(
  Math.max(
    sentBubble.box.x + sentBubble.box.width,
    ...collectNodes(sentBubble)
      .filter((node) => node.type === "message_bubble_tail" && node.box)
      .map((node) => node.box!.x + node.box!.width),
  ) === messageAreaBox.x + messageAreaBox.width,
  "Sent bubble plus tail must align to the scaled message-area right edge",
);
assert(
  receivedBubble.box.y + receivedBubble.box.height <= sentBubble.box.y,
  "Message bubbles must stack without overlap",
);
const avatarNodes = collectNodes(tree).filter((node) => node.type === "avatar");
assert(avatarNodes.length > 0, "Resolved avatars must produce avatar nodes");
assert(
  avatarNodes.every((node) => node.box !== undefined),
  "Every rendered avatar must have a box",
);
assert(
  avatarNodes.some((node) => node.style?.surfaceRelief),
  "Avatar component must consume theme surface relief when enabled",
);
assert(
  collectNodes(tree).some(
    (node) => node.type === "chat_header_icon" && node.style?.buttonIcon,
  ),
  "Chat header icons must consume the shared button_icon component",
);
assert(
  collectNodes(tree).some(
    (node) => node.type === "message_bubble_shape" && node.style?.surfaceRelief,
  ),
  "Message bubble shape groups must consume theme surface relief when enabled",
);
assert(
  bubbleNodes.every((bubble) =>
    bubble.children?.some(
      (child) => child.type === "text" && child.box !== undefined,
    ),
  ),
  "Every bubble must contain a measured text box",
);
const messageTextNodes = collectNodes(tree).filter(
  (node) => node.role === "message_text",
);
const expectedMessageTypography = isRecord(chatProps.theme.typography?.message)
  ? chatProps.theme.typography.message
  : {};
assert(
  messageTextNodes.every(
    (node) =>
      node.style?.fontWeight === expectedMessageTypography?.fontWeight &&
      node.style?.fontStyle === expectedMessageTypography?.fontStyle,
  ),
  "Message text nodes must inherit app/theme typography font weight and style",
);
const headerTitleNode = collectNodes(tree).find(
  (node) => node.id === "chat_header:title",
);
const expectedHeaderTitleTypography = isRecord(
  chatProps.theme.typography?.headerTitle,
)
  ? chatProps.theme.typography.headerTitle
  : {};
assert(
  headerTitleNode?.style?.fontSize === expectedHeaderTitleTypography.fontSize &&
    headerTitleNode?.style?.fontWeight === expectedHeaderTitleTypography.fontWeight &&
    headerTitleNode?.style?.fontStyle === expectedHeaderTitleTypography.fontStyle,
  "Chat header title must inherit scaled app/theme typography tokens",
);
const statusItems =
  statusNode?.children?.flatMap((child) => child.children ?? []) ?? [];
const wifiStatusItem = statusItems.find(
  (child) =>
    child.type === "status_bar_item" && child.metadata?.id === "wifi",
);
assert(
  wifiStatusItem?.metadata?.token === "status_wifi" &&
    wifiStatusItem.role === "iconToken",
  "Status bar must receive configured Wi-Fi icon item",
);
assert(
  JSON.stringify(tree) === JSON.stringify(secondTree),
  "Visual module output must be deterministic for identical props/frame",
);
const overflowProps = ResolvedChatScreenPropsSchema.parse({
  ...chatProps,
  messages: Array.from({ length: 80 }, (_, index) => ({
    ...chatProps.messages[index % chatProps.messages.length],
    id: `overflow_message_${index}`,
  })),
});
const overflowTree = RenderableNodeSchema.parse(chatModule.render(overflowProps));
const overflowLayout = overflowTree.metadata?.layout;
assert(
  isRecord(overflowLayout) && isRecord(overflowLayout.overflow),
  "Overflow tree must expose overflow metadata",
);
assert(
  overflowLayout.overflow.hasOverflow === true &&
    typeof overflowLayout.overflow.scrollOffset === "number" &&
    overflowLayout.overflow.scrollOffset > 0,
  "Overflow policy must compute a positive deterministic scroll offset",
);
const overflowMessageListBox = overflowLayout.messageListBox;
const overflowBubbles = collectNodes(overflowTree).filter(
  (child) => child.type === "message_bubble",
);
const lastOverflowBubble = overflowBubbles.at(-1);
assert(
  isBox(overflowMessageListBox) &&
    lastOverflowBubble?.box !== undefined &&
    lastOverflowBubble.box.y + lastOverflowBubble.box.height <=
      overflowMessageListBox.y + overflowMessageListBox.height,
  "Keep-latest-visible policy must leave the final bubble inside the list area",
);
const zeroGutterProps = ResolvedChatScreenPropsSchema.parse({
  ...chatProps,
  theme: {
    ...chatProps.theme,
    layout: {
      ...chatProps.theme.layout,
      screenGutter: 0,
    },
  },
});
const zeroGutterTree = RenderableNodeSchema.parse(
  chatModule.render(zeroGutterProps),
);
const zeroGutterLayout = zeroGutterTree.metadata?.layout;
const zeroGutterMessageListBox = isRecord(zeroGutterLayout)
  ? zeroGutterLayout.messageListBox
  : undefined;
const zeroGutterSentBubble = collectNodes(zeroGutterTree).find(
  (child) => child.type === "message_bubble" && child.role === "outgoing",
);
const zeroGutterSentBubbleRight = zeroGutterSentBubble
  ? Math.max(
      zeroGutterSentBubble.box?.x ?? 0,
      zeroGutterSentBubble.box
        ? zeroGutterSentBubble.box.x + zeroGutterSentBubble.box.width
        : 0,
      ...collectNodes(zeroGutterSentBubble)
        .filter((node) => node.type === "message_bubble_tail" && node.box)
        .map((node) => node.box!.x + node.box!.width),
    )
  : 0;
assert(
  isBox(zeroGutterMessageListBox) && zeroGutterSentBubble?.box,
  "Zero-gutter validation must expose message-list and sent bubble boxes",
);
assert(
  zeroGutterSentBubbleRight <=
    zeroGutterMessageListBox.x + zeroGutterMessageListBox.width,
  "Zero-gutter sent bubble plus tail must not overflow the message-list right edge",
);
assert(
  zeroGutterSentBubbleRight ===
    zeroGutterMessageListBox.x + zeroGutterMessageListBox.width,
  "Zero-gutter sent bubble plus tail must align to the message-list right edge",
);
const audioMessageComponent = isRecord(chatProps.theme.components)
  && isRecord(chatProps.theme.components.audioMessage)
  ? chatProps.theme.components.audioMessage
  : {};
const audioMediaProps = ResolvedChatScreenPropsSchema.parse({
  ...chatProps,
  messages: [
    {
      ...chatProps.messages[0],
      id: "audio_message_validation",
      text: "",
      visibleText: "",
      media: {
        type: "audio",
        durationSeconds: 12,
        playStartFrame: 0,
        frame: 90,
        window: {
          width:
            typeof audioMessageComponent.width === "number"
              ? audioMessageComponent.width
              : 260,
          height:
            typeof audioMessageComponent.height === "number"
              ? audioMessageComponent.height
              : 58,
          offsetX: 0,
          offsetY: 0,
        },
      },
    },
  ],
});
const audioMediaTree = RenderableNodeSchema.parse(
  chatModule.render(audioMediaProps),
);
const audioNodes = collectNodes(audioMediaTree);
assert(
  audioNodes.some((node) => node.type === "message_bubble_audio_play") &&
    audioNodes.some((node) => node.type === "message_bubble_audio_waveform_bar") &&
    audioNodes.some((node) => node.type === "message_bubble_audio_progress_knob") &&
    audioNodes.some((node) => node.type === "message_bubble_audio_badge"),
  "Audio media messages must render play, deterministic waveform, progress knob, and microphone badge",
);
assert(
  audioNodes.some(
    (node) => node.type === "message_bubble_audio_play" && node.text === "Ⅱ",
  ),
  "Audio media play control must show pause while playback is active",
);
const videoMediaProps = ResolvedChatScreenPropsSchema.parse({
  ...chatProps,
  messages: [
    {
      ...chatProps.messages[0],
      id: "video_message_validation",
      text: "",
      visibleText: "",
      timing: {
        startFrame: chatProps.frame,
        enterDurationFrames: 0,
      },
      media: {
        type: "video",
        uri: "file:///tmp/validation-video.mp4",
        durationSeconds: 12,
        playStartFrame: 0,
        frame: 0,
        window: {
          width: 260,
          height: 146,
          offsetX: 0,
          offsetY: 0,
        },
      },
    },
  ],
});
const videoMediaTree = RenderableNodeSchema.parse(
  chatModule.render(videoMediaProps),
);
const videoNodes = collectNodes(videoMediaTree);
assert(
  videoNodes.some((node) => node.type === "message_bubble_video_status_icon") &&
    videoNodes.some(
      (node) =>
        node.type === "message_bubble_video_status_duration" &&
        node.text === "0:12",
    ) &&
    videoNodes.some((node) => node.type === "message_bubble_video_play_overlay"),
  "Video media messages must render status icon, duration, and pre-play overlay",
);
const keyboardProps = ResolvedChatScreenPropsSchema.parse({
  ...chatProps,
  props: {
    ...chatProps.props,
    showKeyboard: true,
    keyboard: {
      pressedKey: "A",
    },
  },
  keyboard: {
    ...chatProps.keyboard,
    mode: "shift",
    pressedKey: "A",
    rows: parseKeyboardRows(STANDARD_IOS_KEYBOARD_LAYOUT, "shift", "es"),
  },
});
const keyboardTree = RenderableNodeSchema.parse(chatModule.render(keyboardProps));
const keyboardNode = keyboardTree.children?.find(
  (child) => child.type === "keyboard",
);
const keyboardRows = collectNodes(keyboardTree).filter(
  (node) => node.type === "keyboard_row",
);
const keyboardKeys = collectNodes(keyboardTree).filter(
  (node) => node.type === "keyboard_key",
);
const keyboardPopover = collectNodes(keyboardTree).find(
  (node) => node.type === "keyboard_key_popover",
);
assert(keyboardNode?.box, "Keyboard must render a box when showKeyboard=true");
assert(
  keyboardRows.length >= 4 && keyboardKeys.length > 20,
  "Keyboard must render rows and generated key nodes",
);
assert(
  keyboardNode.metadata?.mode === "shift" && keyboardPopover?.text === "A",
  "Keyboard must infer shift mode and render the Apple-style key popover",
);
assert(
  keyboardKeys.some((node) => node.style?.surfaceRelief),
  "Keyboard keys must consume theme surface relief when enabled",
);
assert(
  collectNodes(keyboardTree).some(
    (node) => node.type === "keyboard_bottom_item" && node.style?.buttonIcon,
  ),
  "Keyboard bottom icons must consume the shared button_icon component",
);
const activeComposerId =
  typeof chatProps.props.activeComposerMessageId === "string"
    ? chatProps.props.activeComposerMessageId
    : "";
const activeComposer = chatProps.messages.find(
  (message) => message.id === activeComposerId,
);
assert(
  activeComposer?.timing.writeOnStartFrame !== undefined,
  "Keyboard push validation requires an active write-on message",
);
const pushDurationFrames = 8;
const keyboardEnterStartFrame = Math.max(
  0,
  activeComposer.timing.writeOnStartFrame - pushDurationFrames,
);
const keyboardPushStartTree = RenderableNodeSchema.parse(
  chatModule.render(
    ResolvedChatScreenPropsSchema.parse({
      ...chatProps,
      frame: keyboardEnterStartFrame,
      props: {
        ...chatProps.props,
        showKeyboard: true,
        showTextInputBar: true,
      },
      keyboard: {
        ...chatProps.keyboard,
        pushDurationFrames,
      },
    }),
  ),
);
const keyboardPushEndTree = RenderableNodeSchema.parse(
  chatModule.render(
    ResolvedChatScreenPropsSchema.parse({
      ...chatProps,
      frame: activeComposer.timing.writeOnStartFrame,
      props: {
        ...chatProps.props,
        showKeyboard: true,
        showTextInputBar: true,
      },
      keyboard: {
        ...chatProps.keyboard,
        pushDurationFrames,
      },
    }),
  ),
);
const keyboardPushStartNode = keyboardPushStartTree.children?.find(
  (child) => child.type === "keyboard",
);
const keyboardPushEndNode = keyboardPushEndTree.children?.find(
  (child) => child.type === "keyboard",
);
const textInputIdleTree = RenderableNodeSchema.parse(
  chatModule.render(
    ResolvedChatScreenPropsSchema.parse({
      ...chatProps,
      props: {
        ...chatProps.props,
        showKeyboard: false,
        showTextInputBar: true,
      },
    }),
  ),
);
const textInputIdleNode = textInputIdleTree.children?.find(
  (child) => child.type === "text_input_bar",
);
const textInputPushStartNode = keyboardPushStartTree.children?.find(
  (child) => child.type === "text_input_bar",
);
assert(
  keyboardPushStartNode?.box &&
    keyboardPushEndNode?.box &&
    keyboardPushStartNode.box.y > keyboardPushEndNode.box.y,
  "Keyboard must push in from below during its configured duration",
);
assert(
  textInputIdleNode?.box &&
    textInputPushStartNode?.box &&
    textInputPushStartNode.box.y <= textInputIdleNode.box.y,
  "Text input bar must never animate below its idle position above the navigation bar",
);
const keyboardPushExitStartTree = RenderableNodeSchema.parse(
  chatModule.render(
    ResolvedChatScreenPropsSchema.parse({
      ...chatProps,
      frame:
        activeComposer.timing.writeOnStartFrame +
        (activeComposer.timing.writeOnDurationFrames ?? 0),
      props: {
        ...chatProps.props,
        showKeyboard: true,
        showTextInputBar: true,
        keyboardTransition: {
          phase: "exit",
          startFrame:
            activeComposer.timing.writeOnStartFrame +
            (activeComposer.timing.writeOnDurationFrames ?? 0),
        },
      },
      keyboard: {
        ...chatProps.keyboard,
        pushDurationFrames,
      },
    }),
  ),
);
const keyboardPushExitMidTree = RenderableNodeSchema.parse(
  chatModule.render(
    ResolvedChatScreenPropsSchema.parse({
      ...chatProps,
      frame:
        activeComposer.timing.writeOnStartFrame +
        (activeComposer.timing.writeOnDurationFrames ?? 0) +
        Math.ceil(pushDurationFrames / 2),
      props: {
        ...chatProps.props,
        showKeyboard: true,
        showTextInputBar: true,
        keyboardTransition: {
          phase: "exit",
          startFrame:
            activeComposer.timing.writeOnStartFrame +
            (activeComposer.timing.writeOnDurationFrames ?? 0),
        },
      },
      keyboard: {
        ...chatProps.keyboard,
        pushDurationFrames,
      },
    }),
  ),
);
const keyboardPushExitStartNode = keyboardPushExitStartTree.children?.find(
  (child) => child.type === "keyboard",
);
const keyboardPushExitMidNode = keyboardPushExitMidTree.children?.find(
  (child) => child.type === "keyboard",
);
assert(
  keyboardPushExitStartNode?.box &&
    keyboardPushExitMidNode?.box &&
    keyboardPushExitMidNode.box.y > keyboardPushExitStartNode.box.y,
  "Keyboard must push out below with the same configured duration",
);
const textInputProps = ResolvedChatScreenPropsSchema.parse({
  ...chatProps,
  props: {
    ...chatProps.props,
    showTextInputBar: true,
    textInputBar: {
      text: "Hola",
      state: "typing",
    },
  },
  textInputBar: {
    ...(chatProps.textInputBar ?? {}),
    text: "Hola",
    state: "typing",
    layout: {
      height: 56,
      paddingX: 8,
      paddingY: 6,
      gap: 8,
      fieldHeight: 40,
      fieldPaddingX: 14,
      fieldRadius: 20,
      iconSize: 24,
      fontSize: 17,
      cursorWidth: 2,
    },
    leftItems: [{ id: "emoji", token: "chat_emoji", order: 10 }],
    rightItems: [
      {
        id: "send",
        token: "chat_send",
        order: 10,
        color: "#007AFF",
      },
    ],
    cursorVisible: true,
  },
});
const textInputTree = RenderableNodeSchema.parse(
  chatModule.render(textInputProps),
);
const textInputNode = collectNodes(textInputTree).find(
  (node) => node.type === "text_input_bar",
);
const textInputField = collectNodes(textInputTree).find(
  (node) => node.type === "text_input_bar_field",
);
const textInputCursor = collectNodes(textInputTree).find(
  (node) => node.type === "text_input_bar_cursor",
);
const textInputSend = collectNodes(textInputTree).find(
  (node) =>
    node.type === "text_input_bar_item" && node.metadata?.token === "chat_send",
);
assert(textInputNode?.box, "Text input bar must render a box when enabled");
assert(
  textInputField?.text === "Hola" && textInputCursor,
  "Text input bar must render draft text with cursor",
);
assert(
  textInputSend?.style?.color === "#007AFF",
  "Text input bar must support state-specific colored icons",
);
assert(
  textInputSend?.style?.buttonIcon,
  "Text input bar icons must consume the shared button_icon component",
);
assert(
  Object.keys(visualModuleRegistry).sort().join(",") ===
    "avatar,chat_header,chat_screen,keyboard,message_bubble,navigation_bar,status_bar,text_input_bar",
  "Registry must contain all required visual modules",
);

console.log("✓ resolved chat props rendered at shot frame 60 / local frame 60");
console.log("✓ renderable tree validated recursively with Zod");
console.log("✓ ChatScreen composed status bar, navigation bar, header, and message bubbles");
console.log("✓ app wallpaper rendered as the back layer");
console.log("✓ registry contains all seven required module names");
console.log("✓ visual tree uses resolved layout, tail, and Wi-Fi tokens");
console.log("✓ sent/received bounds, stacking, text, and avatar boxes validated");
console.log("✓ repeated rendering produced an identical tree");
console.log("✓ deterministic overflow keeps the latest message visible");
console.log("✓ zero-gutter sent bubble aligns to the right edge without clipping");
console.log("✓ text input bar renders state-specific icons, draft text, and cursor");
console.log(
  `layout: chat_screen ${tree.box.x},${tree.box.y} ${tree.box.width}x${tree.box.height}`,
);
for (const child of [wallpaperNode, statusNode, headerNode, ...bubbleNodes]) {
  if (child?.box) {
    console.log(
      `  ${child.type}${child.role ? ` ${child.role}` : ""} ${child.box.x},${child.box.y} ${child.box.width}x${child.box.height}`,
    );
  }
}
console.log("Renderer-agnostic visual module validation succeeded.");
