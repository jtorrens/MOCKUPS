import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  centerBox,
  numberToken,
  renderScale,
  selectedColor,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  approximateMultilineTextSize,
  approximateWrappedTextLines,
  approximateWrappedTextSize,
  resolveTypographyStyle,
} from "./previewTextHelpers.js";
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
  const hasLeftIcons = textBox.leftIconRow.icons.length > 0;
  const hasRightIcons = textBox.rightIconRow.icons.length > 0;
  const contentText = visibleText(textBox);
  const contentSize = approximateMultilineTextSize(
    contentText,
    typography.fontSize,
    typography.lineHeight,
  );
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
      contentTextHeight: contentSize.height,
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
    let wrappedContentSize = approximateWrappedTextSize(
      contentText,
      typography.fontSize,
      typography.lineHeight,
      Math.max(1, width - paddingX * 2 - iconTextInset(
        hasLeftIcons,
        hasRightIcons,
        leftIconSize.width,
        rightIconSize.width,
        iconGap,
      ).total),
    );
    let height = growingHeight(
      minimumHeight,
      paddingY,
      typography.lineHeight,
      textBox.maxLines,
      wrappedContentSize.height,
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
    wrappedContentSize = approximateWrappedTextSize(
      contentText,
      typography.fontSize,
      typography.lineHeight,
      Math.max(1, width - paddingX * 2 - iconInset.total),
    );
    height = growingHeight(
      minimumHeight,
      paddingY,
      typography.lineHeight,
      textBox.maxLines,
      wrappedContentSize.height,
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
      contentTextHeight: wrappedContentSize.height,
    };
  }

  const maximumWidth = Math.max(1, textBox.size.width * scale);
  const iconInsetAtMaximum = iconTextInset(
    hasLeftIcons,
    hasRightIcons,
    leftIconSize.width,
    rightIconSize.width,
    iconGap,
  );
  let height = Math.max(1, contentSize.height + paddingY * 2);
  let paddingX = basePaddingX + effectiveCornerTextInset(cornerRadius, maximumWidth, height);
  let wrappedContentSize = approximateWrappedTextSize(
    contentText,
    typography.fontSize,
    typography.lineHeight,
    Math.max(1, maximumWidth - paddingX * 2 - iconInsetAtMaximum.total),
  );
  height = Math.max(
    1,
    wrappedContentSize.height + paddingY * 2,
    Math.max(leftIconSize.height, rightIconSize.height) + paddingY * 2,
  );
  paddingX = basePaddingX + effectiveCornerTextInset(cornerRadius, maximumWidth, height);
  const iconInset = iconTextInset(
    hasLeftIcons,
    hasRightIcons,
    leftIconSize.width,
    rightIconSize.width,
    iconGap,
  );
  wrappedContentSize = approximateWrappedTextSize(
    contentText,
    typography.fontSize,
    typography.lineHeight,
    Math.max(1, maximumWidth - paddingX * 2 - iconInset.total),
  );
  height = Math.max(
    1,
    wrappedContentSize.height + paddingY * 2,
    Math.max(leftIconSize.height, rightIconSize.height) + paddingY * 2,
  );
  const width = Math.min(
    maximumWidth,
    Math.max(1, wrappedContentSize.width + paddingX * 2 + iconInset.total),
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
    contentTextHeight: wrappedContentSize.height,
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
  const textIsEmpty = textBox.text.trim().length === 0;
  const cursorWidth = Math.max(1, textBox.cursor.width * scale);
  const wrappedLines = approximateWrappedTextLines(
    size.contentText,
    size.typography.fontSize,
    textFrame.width,
  );
  const iconY = (iconHeight: number) =>
    wrappedLines.length <= 1
      ? box.y + Math.max(0, (box.height - iconHeight) * 0.5)
      : box.y + box.height - size.paddingY - iconHeight;
  const lineHeight = size.typography.lineHeight;
  const textContentHeight = Math.max(1, wrappedLines.length) * lineHeight;
  const textOverflowsFrame = textContentHeight > textFrame.height;
  const scrollAnchorsToBottom = textOverflowsFrame && textBox.overflowMode === "scroll";
  const textContentY = wrappedLines.length === 1
      ? textFrame.y + Math.max(0, (textFrame.height - textContentHeight) * 0.5)
      : textFrame.y;
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
    whiteSpace: "pre-line",
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
        children: [
          scrollAnchorsToBottom
            ? {
                id: `${textBox.id}.text`,
                type: "text",
                frame: 0,
                text: size.contentText,
                style: {
                  ...textStyle,
                  width: "100%",
                },
                metadata: textBox.cursorVisible && !textIsEmpty
                  ? {
                      inlineCursor: {
                        color: selectedColor(payload, textBox.cursor.colorToken),
                        width: cursorWidth,
                        opacity: 1,
                      },
                    }
                  : undefined,
              }
            : {
                id: `${textBox.id}.text`,
                type: "text",
                frame: 0,
                box: {
                  x: textFrame.x,
                  y: textContentY,
                  width: textFrame.width,
                  height: Math.max(textFrame.height, textContentHeight),
                },
                text: size.contentText,
                style: textStyle,
                metadata: textBox.cursorVisible && !textIsEmpty
                  ? {
                      inlineCursor: {
                        color: selectedColor(payload, textBox.cursor.colorToken),
                        width: cursorWidth,
                        opacity: 1,
                      },
                    }
                  : undefined,
              },
        ],
      },
    ],
  };
}

function visibleText(textBox: TextBoxDesignContract) {
  return textBox.text.trim().length > 0 ? textBox.text : textBox.placeholder;
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
  maxLines: number,
  contentHeight: number,
  iconHeight: number,
) {
  const maximumContentHeight = Math.max(1, Math.floor(maxLines)) * lineHeight;
  const visibleContentHeight = Math.min(contentHeight, maximumContentHeight);
  return Math.max(1, minimumHeight, visibleContentHeight + paddingY * 2, iconHeight + paddingY * 2);
}
