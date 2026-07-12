import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  centerBox,
  numberToken,
  renderScale,
  selectedColor,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  measuredMultilineTextSize,
  measuredTextWidth,
  measuredWrappedTextLines,
  measuredWrappedTextSize,
  resolveTypographyStyle,
  type ResolvedTypographyStyle,
} from "./previewTextHelpers.js";
import { textGraphemes } from "./previewTextRevealHelpers.js";
import {
  iconRowComponentToRenderableAt,
  measureIconRowComponent,
} from "./iconRowComponentRenderable.js";
import {
  surfaceComponentToRenderableAt,
  surfaceComponentToRenderableAtWithColors,
  type SurfaceColorOverride,
} from "./surfaceComponentRenderable.js";
import type { TextBoxDesignContract } from "./textBoxComponentContract.js";

export interface TextBoxColorOverride {
  textColor?: string;
  placeholderColor?: string;
}

export interface TextBoxRenderableOptions {
  surfaceVisible?: boolean;
  surfaceColors?: SurfaceColorOverride;
  textColors?: TextBoxColorOverride;
}

export function measureTextBoxComponent(
  payload: DesignPreviewPayload,
  textBox: TextBoxDesignContract,
) {
  const scale = renderScale(payload);
  const typography = resolveTypographyStyle(payload, textBox.typography, scale);
  const cornerRadius = Math.max(
    0,
    numberToken(payload, textBox.surface.surface.cornerRadiusToken) * scale,
  );
  const basePaddingX = numberToken(payload, textBox.padding.xToken) * scale;
  const paddingY = numberToken(payload, textBox.padding.yToken) * scale;
  const iconGap = Math.max(0, numberToken(payload, textBox.iconGapToken) * scale);
  const leftIconSize = measureIconRowComponent(payload, textBox.leftIconRow);
  const rightIconSize = measureIconRowComponent(payload, textBox.rightIconRow);
  const hasLeftIcons = textBox.leftIconRow.items.length > 0;
  const hasRightIcons = textBox.rightIconRow.items.length > 0;
  const contentText = visibleText(textBox);
  const cursorWidth = inlineCursorMeasuredWidth(textBox, typography.fontSize, scale);
  const contentSize = withInlineCursorWidth(
    measuredMultilineTextSize(contentText, typography),
    cursorWidth,
  );
  const contentVisualHeight = textContentVisualHeight(contentSize.lineCount, typography);
  if (textBox.dimensionMode === "fixed") {
    const width = textBox.size.width * scale;
    const height = textBox.size.height * scale;
    const paddingX = basePaddingX + effectiveCornerTextInset(cornerRadius, width, height);
    const iconInset = iconTextInset(
      hasLeftIcons,
      hasRightIcons,
      leftIconSize.width,
      rightIconSize.width,
      iconGap,
    );
    return {
      width,
      height,
      typography,
      basePaddingX,
      paddingX,
      paddingY,
      cornerRadius,
      iconGap,
      leftIconSize,
      rightIconSize,
      hasLeftIcons,
      hasRightIcons,
      iconInset,
      contentText,
      contentTextHeight: contentVisualHeight,
      contentLineCount: contentSize.lineCount,
    };
  }

  if (textBox.dimensionMode === "growVertical") {
    const width = textBox.size.width * scale;
    const minimumHeight = textBox.size.height * scale;
    let paddingX = basePaddingX + effectiveCornerTextInset(
      cornerRadius,
      width,
      minimumHeight,
    );
    let wrappedContentSize = withInlineCursorWidth(
      measuredWrappedTextSize(
        contentText,
        typography,
        resolvedWrapWidth(Math.max(1, width - paddingX * 2 - iconTextInset(
          hasLeftIcons,
          hasRightIcons,
          leftIconSize.width,
          rightIconSize.width,
          iconGap,
        ).total)),
      ),
      cursorWidth,
    );
    let height = growingHeight(
      minimumHeight,
      paddingY,
      typography.lineHeight,
      typography.fontSize,
      textBox.maxLines,
      textContentVisualHeight(wrappedContentSize.lineCount, typography),
      Math.max(leftIconSize.height, rightIconSize.height),
    );
    paddingX = basePaddingX + effectiveCornerTextInset(cornerRadius, width, height);
    const iconInset = iconTextInset(
      hasLeftIcons,
      hasRightIcons,
      leftIconSize.width,
      rightIconSize.width,
      iconGap,
    );
    wrappedContentSize = withInlineCursorWidth(
      measuredWrappedTextSize(
        contentText,
        typography,
        resolvedWrapWidth(Math.max(1, width - paddingX * 2 - iconInset.total)),
      ),
      cursorWidth,
    );
    height = growingHeight(
      minimumHeight,
      paddingY,
      typography.lineHeight,
      typography.fontSize,
      textBox.maxLines,
      textContentVisualHeight(wrappedContentSize.lineCount, typography),
      Math.max(leftIconSize.height, rightIconSize.height),
    );

    return {
      width,
      height,
      typography,
      basePaddingX,
      paddingX,
      paddingY,
      cornerRadius,
      iconGap,
      leftIconSize,
      rightIconSize,
      hasLeftIcons,
      hasRightIcons,
      iconInset,
      contentText,
      contentTextHeight: textContentVisualHeight(wrappedContentSize.lineCount, typography),
      contentLineCount: wrappedContentSize.lineCount,
    };
  }

  const maximumWidth = Math.max(1, textBox.size.width * scale);
  let width = maximumWidth;
  let height = Math.max(1, contentSize.height + paddingY * 2);
  let paddingX = basePaddingX + effectiveCornerTextInset(cornerRadius, width, height);
  let iconInset = iconTextInset(
    hasLeftIcons,
    hasRightIcons,
    leftIconSize.width,
    rightIconSize.width,
    iconGap,
  );
  let naturalWidth = measuredContentWidth(contentSize.width) + paddingX * 2 + iconInset.total;
  let wraps = naturalWidth > maximumWidth;
  let measuredContentSize = wraps
    ? withInlineCursorWidth(
        measuredWrappedTextSize(
          contentText,
          typography,
          resolvedWrapWidth(Math.max(1, maximumWidth - paddingX * 2 - iconInset.total)),
        ),
        cursorWidth,
      )
    : contentSize;

  width = wraps ? maximumWidth : Math.max(1, naturalWidth);
  height = Math.max(
    1,
    textContentVisualHeight(measuredContentSize.lineCount, typography) + paddingY * 2,
    Math.max(leftIconSize.height, rightIconSize.height) + paddingY * 2,
  );
  paddingX = basePaddingX + effectiveCornerTextInset(cornerRadius, width, height);
  iconInset = iconTextInset(
    hasLeftIcons,
    hasRightIcons,
    leftIconSize.width,
    rightIconSize.width,
    iconGap,
  );
  naturalWidth = measuredContentWidth(contentSize.width) + paddingX * 2 + iconInset.total;
  wraps = naturalWidth > maximumWidth;
  measuredContentSize = wraps
    ? withInlineCursorWidth(
        measuredWrappedTextSize(
          contentText,
          typography,
          resolvedWrapWidth(Math.max(1, maximumWidth - paddingX * 2 - iconInset.total)),
        ),
        cursorWidth,
      )
    : contentSize;
  width = wraps ? maximumWidth : Math.max(1, naturalWidth);
  height = Math.max(
    1,
    textContentVisualHeight(measuredContentSize.lineCount, typography) + paddingY * 2,
    Math.max(leftIconSize.height, rightIconSize.height) + paddingY * 2,
  );
  return {
    width,
    height,
    typography,
    basePaddingX,
    paddingX,
    paddingY,
    cornerRadius,
    iconGap,
    leftIconSize,
    rightIconSize,
    hasLeftIcons,
    hasRightIcons,
    iconInset,
    contentText,
    contentTextHeight: textContentVisualHeight(measuredContentSize.lineCount, typography),
    contentLineCount: measuredContentSize.lineCount,
  };
}

export function textBoxComponentToRenderable(
  payload: DesignPreviewPayload,
  textBox: TextBoxDesignContract,
): RenderableNode {
  const size = measureTextBoxComponent(payload, textBox);
  return textBoxComponentToRenderableAt(
    payload,
    textBox,
    centerBox(payload, size.width, size.height),
  );
}

export function textBoxComponentToRenderableAt(
  payload: DesignPreviewPayload,
  textBox: TextBoxDesignContract,
  box: RenderableBox,
  options: TextBoxRenderableOptions = {},
): RenderableNode {
  const scale = renderScale(payload);
  const size = measureTextBoxComponent(payload, textBox);
  const paddingX = size.basePaddingX + effectiveCornerTextInset(size.cornerRadius, box.width, box.height);
  const iconInset = iconTextInset(
    size.hasLeftIcons,
    size.hasRightIcons,
    size.leftIconSize.width,
    size.rightIconSize.width,
    size.iconGap,
  );
  const textFrame = {
    x: box.x + paddingX + iconInset.left,
    y: box.y + size.paddingY,
    width: Math.max(1, box.width - paddingX * 2 - iconInset.total),
    height: Math.max(1, box.height - size.paddingY * 2),
  };
  const textIsEmpty = textBox.text.length === 0;
  const cursorWidth = Math.max(1, textBox.cursor.width * scale);
  const cursorMetadata = inlineCursorMetadata(payload, textBox, cursorWidth);
  const wrappedLines = measuredWrappedTextLines(
    size.contentText,
    size.typography,
    resolvedWrapWidth(textFrame.width),
  );
  const iconY = (iconHeight: number) =>
    wrappedLines.length <= 1
      ? box.y + Math.max(0, (box.height - iconHeight) * 0.5)
      : box.y + box.height - size.paddingY - iconHeight;
  const lineHeight = size.typography.lineHeight;
  const textLineCount = Math.max(1, wrappedLines.length);
  const textContentHeight = textContentVisualHeight(textLineCount, size.typography);
  const textOverflowsFrame = textContentHeight > textFrame.height + 0.5;
  const scrollAnchorsToBottom = textOverflowsFrame && textBox.overflowMode === "scroll";
  const textContentY = wrappedLines.length === 1
      ? textFrame.y + Math.max(0, (textFrame.height - textContentHeight) * 0.5)
      : textFrame.y;
  const renderedTextY = scrollAnchorsToBottom
    ? textFrame.y + textFrame.height - textContentHeight
    : textContentY;
  const textStyle = {
    textColor: textIsEmpty
      ? options.textColors?.placeholderColor
        ?? selectedColor(payload, textBox.placeholderColorToken)
      : options.textColors?.textColor
        ?? selectedColor(payload, textBox.textColorToken),
    display: "block",
    fontSize: size.typography.fontSize,
    fontFamily: size.typography.fontFamily,
    fontStyle: size.typography.fontStyle,
    fontWeight: size.typography.fontWeight,
    lineHeight: size.typography.lineHeight,
    overflow: "visible",
    textAlign: textBox.textAlign,
    whiteSpace: "pre",
  };
  const surfaceNode = options.surfaceVisible === false
    ? undefined
    : options.surfaceColors
      ? surfaceComponentToRenderableAtWithColors(
          payload,
          textBox.surface,
          box,
          options.surfaceColors,
        )
      : surfaceComponentToRenderableAt(payload, textBox.surface, box);

  return {
    id: textBox.id,
    type: "group",
    frame: 0,
    box,
    style: {
      overflow: "visible",
    },
    children: [
      ...(surfaceNode ? [surfaceNode] : []),
      ...(size.hasLeftIcons
        ? [iconRowComponentToRenderableAt(payload, textBox.leftIconRow, {
            x: box.x + paddingX,
            y: iconY(size.leftIconSize.height),
            width: size.leftIconSize.width,
            height: size.leftIconSize.height,
          })]
        : []),
      ...(size.hasRightIcons
        ? [iconRowComponentToRenderableAt(payload, textBox.rightIconRow, {
            x: box.x + box.width - paddingX - size.rightIconSize.width,
            y: iconY(size.rightIconSize.height),
            width: size.rightIconSize.width,
            height: size.rightIconSize.height,
          })]
        : []),
      {
        id: `${textBox.id}.textClip`,
        type: "group",
        frame: 0,
        box: textFrame,
        style: {
          alignItems: scrollAnchorsToBottom ? "stretch" : undefined,
          display: scrollAnchorsToBottom ? "flex" : undefined,
          flexDirection: scrollAnchorsToBottom ? "column" : undefined,
          justifyContent: scrollAnchorsToBottom ? "flex-end" : undefined,
          overflow: "hidden",
        },
        children: wrappedLines.flatMap((line, index) =>
          textLineRenderableNodes({
            id: `${textBox.id}.text.${index}`,
            textBox,
            line,
            lineIndex: index,
            lineCount: wrappedLines.length,
            lineHeight,
            textFrame,
            y: renderedTextY + index * lineHeight,
            style: textStyle,
            typography: size.typography,
            cursorMetadata: index === wrappedLines.length - 1 ? cursorMetadata : undefined,
          }),
        ),
      },
    ],
  };
}

function textLineRenderableNodes({
  id,
  textBox,
  line,
  lineIndex,
  lineCount,
  lineHeight,
  textFrame,
  y,
  style,
  typography,
  cursorMetadata,
}: {
  id: string;
  textBox: TextBoxDesignContract;
  line: string;
  lineIndex: number;
  lineCount: number;
  lineHeight: number;
  textFrame: RenderableBox;
  y: number;
  style: Record<string, unknown>;
  typography: ResolvedTypographyStyle;
  cursorMetadata: RenderableNode["metadata"];
}): RenderableNode[] {
  if (textBox.textAnimation.mode === "none" || line.length === 0) {
    return [{
      id,
      type: "text",
      frame: 0,
      box: {
        x: textFrame.x,
        y,
        width: textFrame.width,
        height: lineHeight,
      },
      text: line,
      style,
      metadata: cursorMetadata,
    }];
  }

  const fontSize = typography.fontSize;
  const graphemes = textGraphemes(line);
  let x = textFrame.x + lineStartOffset(line, textBox.textAlign, textFrame.width, typography);
  const cycle = Math.max(0, textBox.textAnimation.elapsedMs) / 1000 * Math.PI * 2;
  const minimumOpacity = Math.max(0.35, Math.min(1, textBox.textAnimation.minimumOpacity));
  return graphemes.map((grapheme, graphemeIndex) => {
    const width = Math.max(1, measuredTextWidth(grapheme, typography));
    const phase = cycle + graphemeIndex * 0.72 + lineIndex * 0.29;
    const wave = (Math.sin(phase) + 1) * 0.5;
    const opacity = minimumOpacity + (1 - minimumOpacity) * wave;
    const scale = textBox.textAnimation.mode === "pulsating"
      ? 0.88 + 0.24 * wave
      : 1;
    const yOffset = textBox.textAnimation.mode === "wave"
      ? (0.5 - wave) * fontSize * 0.28
      : 0;
    const node: RenderableNode = {
      id: `${id}.${graphemeIndex}`,
      type: "text",
      frame: 0,
      box: {
        x,
        y,
        width,
        height: lineHeight,
      },
      text: grapheme,
      style: {
        ...style,
        display: "block",
        textAlign: "left",
      },
      transform: {
        y: yOffset,
        scale,
        opacity,
      },
      metadata: lineIndex === lineCount - 1 && graphemeIndex === graphemes.length - 1
        ? cursorMetadata
        : undefined,
    };
    x += width;
    return node;
  });
}

function lineStartOffset(
  line: string,
  textAlign: TextBoxDesignContract["textAlign"],
  frameWidth: number,
  typography: ResolvedTypographyStyle,
) {
  if (textAlign === "left") return 0;
  const lineWidth = measuredTextWidth(line, typography);
  if (textAlign === "center") return Math.max(0, (frameWidth - lineWidth) / 2);
  return Math.max(0, frameWidth - lineWidth);
}

function visibleText(textBox: TextBoxDesignContract) {
  return textBox.text.length > 0 ? textBox.text : textBox.placeholder;
}

function inlineCursorShouldRender(textBox: TextBoxDesignContract) {
  return textBox.cursorVisible
    && (textBox.text.length > 0 || textBox.placeholder.length === 0);
}

function inlineCursorMeasuredWidth(
  textBox: TextBoxDesignContract,
  fontSize: number,
  scale: number,
) {
  if (!inlineCursorShouldRender(textBox)) return 0;
  return Math.max(1, textBox.cursor.width * scale) + Math.max(1, fontSize * 0.01);
}

function inlineCursorMetadata(
  payload: DesignPreviewPayload,
  textBox: TextBoxDesignContract,
  cursorWidth: number,
) {
  return inlineCursorShouldRender(textBox)
    ? {
        inlineCursor: {
          color: selectedColor(payload, textBox.cursor.colorToken),
          width: cursorWidth,
          opacity: 1,
        },
      }
    : undefined;
}

function withInlineCursorWidth<T extends { width: number }>(
  size: T,
  cursorWidth: number,
) {
  if (cursorWidth <= 0) return size;
  return {
    ...size,
    width: size.width + cursorWidth,
  };
}

function effectiveCornerTextInset(cornerRadius: number, width: number, height: number) {
  const effectiveRadius = Math.min(
    Math.max(0, cornerRadius),
    Math.max(0, width) * 0.5,
    Math.max(0, height) * 0.5,
  );
  return Math.min(effectiveRadius * 0.35, Math.max(0, height) * 0.18);
}

function iconTextInset(
  hasLeftIcons: boolean,
  hasRightIcons: boolean,
  leftWidth: number,
  rightWidth: number,
  iconGap: number,
) {
  const left = hasLeftIcons ? leftWidth + iconGap : 0;
  const right = hasRightIcons ? rightWidth + iconGap : 0;
  return {
    left,
    right,
    total: left + right,
  };
}

function growingHeight(
  minimumHeight: number,
  paddingY: number,
  lineHeight: number,
  fontSize: number,
  maxLines: number,
  contentHeight: number,
  iconHeight: number,
) {
  const maximumContentHeight = textContentVisualHeight(
    Math.max(1, Math.floor(maxLines)),
    { fontSize, lineHeight },
  );
  const visibleContentHeight = Math.min(contentHeight, maximumContentHeight);
  return Math.max(1, minimumHeight, visibleContentHeight + paddingY * 2, iconHeight + paddingY * 2);
}

function textContentVisualHeight(
  lineCount: number,
  typography: { fontSize: number; lineHeight: number },
) {
  return Math.max(1, Math.max(1, lineCount) * typography.lineHeight + textMetricSlack(typography.fontSize));
}

function textMetricSlack(fontSize: number) {
  return Math.max(1, fontSize * 0.14);
}

function resolvedWrapWidth(width: number) {
  return Math.max(1, width);
}

function measuredContentWidth(width: number) {
  return Math.max(1, width);
}
