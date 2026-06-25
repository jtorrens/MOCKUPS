import type {
  ResolvedChatScreenProps,
  ResolvedMessageBubbleProps,
} from "../../domain/schemas/index.js";
import { readNumber } from "../renderable/helpers.js";
import type { RenderableBox } from "../renderable/types.js";
import { layoutMessageBubble } from "./layoutMessageBubble.js";
import type {
  ChatMessageLayout,
  ChatScreenLayout,
  MessageBubbleLayout,
} from "./types.js";

export interface LayoutChatScreenInput {
  props: ResolvedChatScreenProps;
  messages: ResolvedMessageBubbleProps[];
}

function translateMessageLayout(
  layout: MessageBubbleLayout,
  offsetY: number,
): MessageBubbleLayout {
  const translateBox = (box: RenderableBox): RenderableBox => ({
    ...box,
    y: box.y - offsetY,
  });
  return {
    ...layout,
    bubbleBox: translateBox(layout.bubbleBox),
    textBox: translateBox(layout.textBox),
    ...(layout.avatarBox
      ? { avatarBox: translateBox(layout.avatarBox) }
      : {}),
  };
}

export function layoutChatScreen({
  props,
  messages,
}: LayoutChatScreenInput): ChatScreenLayout {
  const showStatusBar = props.props.showStatusBar !== false;
  const showNavigationBar = props.props.showNavigationBar !== false;
  const showKeyboard = props.props.showKeyboard === true;
  const showHeader = props.props.showHeader !== false;
  const screenGutter = readNumber(props.theme.layout, "screenGutter", 24);
  const headerHeight = showHeader
    ? readNumber(props.theme.header, "height", 96)
    : 0;
  const messageSpacing = readNumber(props.theme.messages, "spacing", 6);
  const groupSpacing = readNumber(props.theme.messages, "groupSpacing", 12);
  const rootBox = {
    x: props.viewport.x,
    y: props.viewport.y,
    width: props.viewport.width,
    height: props.viewport.height,
  };
  const statusBarHeight = showStatusBar ? props.device.statusBarHeight : 0;
  const navigationBarLayout =
    typeof props.navigationBar?.layout === "object" &&
    props.navigationBar.layout !== null &&
    !Array.isArray(props.navigationBar.layout)
      ? (props.navigationBar.layout as Record<string, unknown>)
      : {};
  const navigationBarHeight =
    showNavigationBar && typeof navigationBarLayout.height === "number"
      ? navigationBarLayout.height
      : 0;
  const keyboardLayout =
    typeof props.keyboard?.layout === "object" &&
    props.keyboard.layout !== null &&
    !Array.isArray(props.keyboard.layout)
      ? (props.keyboard.layout as Record<string, unknown>)
      : {};
  const keyboardHeight =
    showKeyboard && typeof keyboardLayout.height === "number"
      ? keyboardLayout.height
      : 0;
  const statusBarBox = showStatusBar
    ? {
        x: rootBox.x,
        y: rootBox.y,
        width: rootBox.width,
        height: statusBarHeight,
      }
    : undefined;
  const headerBox = showHeader
    ? {
        x: rootBox.x,
        y: rootBox.y + statusBarHeight,
        width: rootBox.width,
        height: headerHeight,
      }
    : undefined;
  const navigationBarBox =
    showNavigationBar && navigationBarHeight > 0
      ? {
          x: rootBox.x,
          y: rootBox.y + rootBox.height - navigationBarHeight,
          width: rootBox.width,
          height: navigationBarHeight,
        }
      : undefined;
  const keyboardBox =
    showKeyboard && keyboardHeight > 0
      ? {
          x: rootBox.x,
          y: rootBox.y + rootBox.height - navigationBarHeight - keyboardHeight,
          width: rootBox.width,
          height: keyboardHeight,
        }
      : undefined;
  const messageListTop = rootBox.y + statusBarHeight + headerHeight;
  const messageListBottom =
    rootBox.y +
    rootBox.height -
    keyboardHeight -
    navigationBarHeight -
    props.viewport.safeArea.bottom;
  const messageListLeft = rootBox.x + props.viewport.safeArea.left + screenGutter;
  const messageListRight =
    rootBox.x + rootBox.width - props.viewport.safeArea.right - screenGutter;
  const messageListBox = {
    x: messageListLeft,
    y: messageListTop,
    width: Math.max(1, messageListRight - messageListLeft),
    height: Math.max(0, messageListBottom - messageListTop),
  };

  let cursorY = messageListBox.y + groupSpacing;
  let previousActorId: string | undefined;
  const messageLayouts: ChatMessageLayout[] = messages.map((message) => {
    if (previousActorId !== undefined) {
      cursorY +=
        previousActorId === message.actor.id ? messageSpacing : groupSpacing;
    }
    const layout = layoutMessageBubble({
      props: message,
      messageArea: messageListBox,
      y: cursorY,
    });
    cursorY += layout.bubbleBox.height;
    previousActorId = message.actor.id;
    return { messageId: message.id, layout };
  });

  const contentBottom = messageLayouts.at(-1)?.layout.bubbleBox
    ? messageLayouts.at(-1)!.layout.bubbleBox.y +
      messageLayouts.at(-1)!.layout.bubbleBox.height
    : messageListBox.y;
  const visibleBottom = messageListBox.y + messageListBox.height;
  const scrollOffset = Math.max(0, contentBottom - visibleBottom);
  const translatedLayouts =
    scrollOffset > 0
      ? messageLayouts.map((message) => ({
          ...message,
          layout: translateMessageLayout(message.layout, scrollOffset),
        }))
      : messageLayouts;

  return {
    rootBox,
    ...(statusBarBox ? { statusBarBox } : {}),
    ...(navigationBarBox ? { navigationBarBox } : {}),
    ...(keyboardBox ? { keyboardBox } : {}),
    ...(headerBox ? { headerBox } : {}),
    messageListBox,
    messages: translatedLayouts,
    overflow: {
      hasOverflow: scrollOffset > 0,
      scrollOffset,
      policy: "keep_latest_visible",
    },
  };
}
