import type { RenderableBox } from "../renderable/types.js";

export interface TextMeasurement {
  width: number;
  height: number;
  lineCount: number;
  maxCharsPerLine: number;
  averageGlyphWidth: number;
  strategy: "average_glyph_width";
}

export interface MessageBubbleLayout {
  bubbleBox: RenderableBox;
  textBox: RenderableBox;
  avatarBox?: RenderableBox;
  measurement: TextMeasurement;
  maxBubbleWidth: number;
  alignment: "left" | "center" | "right";
}

export interface ChatMessageLayout {
  messageId: string;
  layout: MessageBubbleLayout;
}

export interface ChatScreenLayout {
  rootBox: RenderableBox;
  statusBarBox?: RenderableBox;
  navigationBarBox?: RenderableBox;
  headerBox?: RenderableBox;
  messageListBox: RenderableBox;
  messages: ChatMessageLayout[];
  overflow: {
    hasOverflow: boolean;
    scrollOffset: number;
    policy: "keep_latest_visible";
  };
}
