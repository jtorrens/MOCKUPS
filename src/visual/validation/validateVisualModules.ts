import shotExample from "../../../docs/examples/shot_lock_to_chat.json" with {
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
  shotFrame: 210,
});
const chatInstance = resolvedShot.active_screen_instances.find(
  (screen) => screen.screen_type === "chat",
);
assert(chatInstance?.resolved_props, "Chat props must resolve at shot frame 210");

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
  sentBubble.box.x + sentBubble.box.width ===
    messageAreaBox.x + messageAreaBox.width,
  "Sent bubble must align to the scaled message-area right edge",
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
assert(
  messageTextNodes.every((node) => node.style?.fontWeight === "Regular"),
  "Message text nodes must receive Chat typography font weight",
);
const headerTitleNode = collectNodes(tree).find(
  (node) => node.id === "chat_header:title",
);
assert(
  headerTitleNode?.style?.fontSize === 51 &&
    headerTitleNode.style.fontWeight === "Semibold",
  "Chat header title must receive scaled Chat typography tokens",
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
assert(
  isBox(zeroGutterMessageListBox) && zeroGutterSentBubble?.box,
  "Zero-gutter validation must expose message-list and sent bubble boxes",
);
assert(
  zeroGutterSentBubble.box.x + zeroGutterSentBubble.box.width <=
    zeroGutterMessageListBox.x + zeroGutterMessageListBox.width,
  "Zero-gutter sent bubble must not overflow the message-list right edge",
);
assert(
  zeroGutterSentBubble.box.x + zeroGutterSentBubble.box.width ===
    zeroGutterMessageListBox.x + zeroGutterMessageListBox.width,
  "Zero-gutter sent bubble must align to the message-list right edge",
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
  Object.keys(visualModuleRegistry).sort().join(",") ===
    "avatar,chat_header,chat_screen,keyboard,message_bubble,navigation_bar,status_bar,text_input_bar",
  "Registry must contain all required visual modules",
);

console.log("✓ resolved chat props rendered at shot frame 210 / local frame 60");
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
