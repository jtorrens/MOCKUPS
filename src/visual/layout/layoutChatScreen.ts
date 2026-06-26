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
  TextMeasurer,
} from "./types.js";

export interface LayoutChatScreenInput {
  props: ResolvedChatScreenProps;
  messages: ResolvedMessageBubbleProps[];
  measurer?: TextMeasurer;
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
    ...(layout.mediaBox ? { mediaBox: translateBox(layout.mediaBox) } : {}),
    textBox: translateBox(layout.textBox),
    ...(layout.statusBox ? { statusBox: translateBox(layout.statusBox) } : {}),
    ...(layout.avatarBox
      ? { avatarBox: translateBox(layout.avatarBox) }
      : {}),
  };
}

function stringValue(value: unknown) {
  return typeof value === "string" ? value : "";
}

export function layoutChatScreen({
  props,
  messages,
  measurer,
}: LayoutChatScreenInput): ChatScreenLayout {
  const showStatusBar = props.props.showStatusBar !== false;
  const showNavigationBar = props.props.showNavigationBar !== false;
  const showKeyboard = props.props.showKeyboard === true;
  const showTextInputBar = props.props.showTextInputBar === true;
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
  const textInputBarLayout =
    typeof props.textInputBar?.layout === "object" &&
    props.textInputBar.layout !== null &&
    !Array.isArray(props.textInputBar.layout)
      ? (props.textInputBar.layout as Record<string, unknown>)
      : {};
  const textInputBarHeight =
    showTextInputBar && typeof textInputBarLayout.height === "number"
      ? textInputBarLayout.height
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
  const textInputBarBox =
    showTextInputBar && textInputBarHeight > 0
      ? {
          x: rootBox.x,
          y:
            rootBox.y +
            rootBox.height -
            navigationBarHeight -
            keyboardHeight -
            textInputBarHeight,
          width: rootBox.width,
          height: textInputBarHeight,
        }
      : undefined;
  const messageListTop = rootBox.y + statusBarHeight + headerHeight;
  const lowerChromeTop =
    textInputBarBox?.y ??
    keyboardBox?.y ??
    navigationBarBox?.y ??
    rootBox.y + rootBox.height - props.viewport.safeArea.bottom;
  const scrollTriggerBottom =
    textInputBarBox?.y ??
    keyboardBox?.y ??
    navigationBarBox?.y ??
    rootBox.y + rootBox.height - props.viewport.safeArea.bottom;
  const messageListBottom = lowerChromeTop;
  const messageListLeft = rootBox.x + props.viewport.safeArea.left + screenGutter;
  const messageListRight =
    rootBox.x + rootBox.width - props.viewport.safeArea.right - screenGutter;
  const messageAreaBox = {
    x: messageListLeft,
    y: messageListTop,
    width: Math.max(1, messageListRight - messageListLeft),
    height: Math.max(0, messageListBottom - messageListTop),
  };
  const messageListBox = {
    x: rootBox.x,
    y: messageListTop,
    width: rootBox.width,
    height: Math.max(0, messageListBottom - messageListTop),
  };

  let cursorY = messageAreaBox.y + groupSpacing;
  const messageLayouts: ChatMessageLayout[] = messages.map((message, index) => {
    if (index > 0) {
      cursorY += messageSpacing;
    }
    const layout = layoutMessageBubble({
      props: message,
      messageArea: messageAreaBox,
      measurer,
      y: cursorY,
    });
    cursorY += layout.bubbleBox.height;
    return { messageId: message.id, layout };
  });

  const activeComposerMessageId = stringValue(props.props.activeComposerMessageId);
  const scrollReferenceLayouts = activeComposerMessageId
    ? messageLayouts.filter((message) => message.messageId !== activeComposerMessageId)
    : messageLayouts;
  const contentBottom = scrollReferenceLayouts.at(-1)?.layout.bubbleBox
    ? scrollReferenceLayouts.at(-1)!.layout.bubbleBox.y +
      scrollReferenceLayouts.at(-1)!.layout.bubbleBox.height
    : messageListBox.y;
  const visibleBottom = messageListBox.y + messageListBox.height;
  const requestedScrollOffset =
    contentBottom > scrollTriggerBottom
      ? Math.max(0, contentBottom - visibleBottom)
      : 0;
  const scrollOffset = requestedScrollOffset;
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
    ...(textInputBarBox ? { textInputBarBox } : {}),
    ...(headerBox ? { headerBox } : {}),
    messageAreaBox,
    messageListBox,
    messages: translatedLayouts,
    overflow: {
      hasOverflow: scrollOffset > 0,
      scrollOffset,
      policy: "keep_latest_visible",
    },
  };
}
