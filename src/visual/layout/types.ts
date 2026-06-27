import type { RenderableBox } from "../renderable/types.js";

export interface TextMeasurement {
  width: number;
  height: number;
  lineCount: number;
  maxCharsPerLine: number;
  averageGlyphWidth: number;
  strategy: "average_glyph_width" | "font_metrics";
}

export interface TextMeasureStyle {
  fontFamily: string;
  fontSize: number;
  fontWeight?: string | number;
}

export interface TextMeasurer {
  measureLineWidth(text: string, style: TextMeasureStyle): number | undefined;
}

export interface MessageBubbleLayout {
  bubbleBox: RenderableBox;
  labelBox?: RenderableBox;
  mediaBox?: RenderableBox;
  textBox: RenderableBox;
  statusBox?: RenderableBox;
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
  keyboardBox?: RenderableBox;
  textInputBarBox?: RenderableBox;
  headerBox?: RenderableBox;
  messageAreaBox: RenderableBox;
  messageListBox: RenderableBox;
  messages: ChatMessageLayout[];
  overflow: {
    hasOverflow: boolean;
    scrollOffset: number;
    policy: "keep_latest_visible";
  };
}
