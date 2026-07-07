import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  centerBox,
  numberToken,
  renderScale,
  selectedColor,
  shadow,
  surfaceVisualPadding,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  approximateMultilineTextSize,
  approximateWrappedTextSize,
  resolveTypographyStyle,
} from "./previewTextHelpers.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import type { TextBoxDesignContract } from "./textBoxComponentContract.js";

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
    return {
      width,
      height,
      typography,
      basePaddingX,
      paddingX,
      paddingY,
      cornerRadius,
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
      Math.max(1, width - paddingX * 2),
    );
    let height = growingHeight(
      minimumHeight,
      paddingY,
      typography.lineHeight,
      textBox.maxLines,
      wrappedContentSize.height,
    );
    paddingX = basePaddingX + effectiveCornerTextInset(cornerRadius, width, height);
    wrappedContentSize = approximateWrappedTextSize(
      contentText,
      typography.fontSize,
      typography.lineHeight,
      Math.max(1, width - paddingX * 2),
    );
    height = growingHeight(
      minimumHeight,
      paddingY,
      typography.lineHeight,
      textBox.maxLines,
      wrappedContentSize.height,
    );

    return {
      width,
      height,
      typography,
      basePaddingX,
      paddingX,
      paddingY,
      cornerRadius,
      contentText,
      contentTextHeight: wrappedContentSize.height,
    };
  }

  const maximumWidth = Math.max(1, textBox.size.width * scale);
  let height = Math.max(1, contentSize.height + paddingY * 2);
  let paddingX = basePaddingX + effectiveCornerTextInset(cornerRadius, maximumWidth, height);
  let wrappedContentSize = approximateWrappedTextSize(
    contentText,
    typography.fontSize,
    typography.lineHeight,
    Math.max(1, maximumWidth - paddingX * 2),
  );
  height = Math.max(1, wrappedContentSize.height + paddingY * 2);
  paddingX = basePaddingX + effectiveCornerTextInset(cornerRadius, maximumWidth, height);
  wrappedContentSize = approximateWrappedTextSize(
    contentText,
    typography.fontSize,
    typography.lineHeight,
    Math.max(1, maximumWidth - paddingX * 2),
  );
  height = Math.max(1, wrappedContentSize.height + paddingY * 2);
  const width = Math.min(
    maximumWidth,
    Math.max(1, wrappedContentSize.width + paddingX * 2),
  );
  return {
    width,
    height,
    typography,
    basePaddingX,
    paddingX,
    paddingY,
    cornerRadius,
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
): RenderableNode {
  const scale = renderScale(payload);
  const size = measureTextBoxComponent(payload, textBox);
  const borderWidth = textBox.surface.surface.borderWidth * scale;
  const surfaceShadow = textBox.surface.surface.shadowEnabled ? shadow(payload) : undefined;
  const surfaceRelief = textBox.surface.surface.reliefEnabled
    ? {
        angleDeg: textBox.surface.surface.reliefAngle,
        extension: textBox.surface.surface.reliefExtent * scale,
        spread: textBox.surface.surface.reliefSpread * scale,
        upperIntensity:
          textBox.surface.surface.reliefTopIntensity * textBox.surface.backgroundAlpha,
        lowerIntensity:
          textBox.surface.surface.reliefBottomIntensity * textBox.surface.backgroundAlpha,
      }
    : undefined;
  const visualPadding = surfaceVisualPadding(borderWidth, surfaceShadow, surfaceRelief);
  const paddingX = size.basePaddingX + effectiveCornerTextInset(size.cornerRadius, box.width, box.height);
  const textFrame = {
    x: box.x + paddingX,
    y: box.y + size.paddingY,
    width: Math.max(1, box.width - paddingX * 2),
    height: Math.max(1, box.height - size.paddingY * 2),
  };
  const textIsEmpty = textBox.text.trim().length === 0;
  const cursorWidth = Math.max(1, textBox.cursor.width * scale);
  const wrappedContentSize = approximateWrappedTextSize(
    size.contentText,
    size.typography.fontSize,
    size.typography.lineHeight,
    textFrame.width,
  );
  const isMultiline = wrappedContentSize.lineCount > 1;
  const textOverflowsFrame = wrappedContentSize.height > textFrame.height;
  const scrollJustification = textOverflowsFrame
    ? "flex-end"
    : isMultiline ? "flex-start" : "center";
  const textContentHeight = Math.max(textFrame.height, wrappedContentSize.height);
  const textNode: RenderableNode = {
    id: `${textBox.id}.text`,
    type: "text",
    frame: 0,
    box: {
      x: textFrame.x,
      y: textFrame.y,
      width: textFrame.width,
      height: textContentHeight,
    },
    text: size.contentText,
    style: {
      textColor: selectedColor(
        payload,
        textIsEmpty ? textBox.placeholderColorToken : textBox.textColorToken,
      ),
      display: isMultiline ? "block" : "flex",
      alignItems: isMultiline ? "flex-start" : "center",
      fontSize: size.typography.fontSize,
      fontFamily: size.typography.fontFamily,
      fontStyle: size.typography.fontStyle,
      fontWeight: size.typography.fontWeight,
      justifyContent: textBoxJustify(textBox.textAlign),
      lineHeight: size.typography.lineHeight,
      overflow: "hidden",
      textAlign: textBox.textAlign,
      whiteSpace: "pre-wrap",
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
  };

  return {
    id: textBox.id,
    type: "group",
    frame: 0,
    box: {
      x: box.x - visualPadding,
      y: box.y - visualPadding,
      width: box.width + visualPadding * 2,
      height: box.height + visualPadding * 2,
    },
    style: {
      overflow: "visible",
    },
    children: [
      surfaceComponentToRenderableAt(payload, textBox.surface, box),
      textBox.overflowMode === "scroll"
        ? {
            id: `${textBox.id}.textClip`,
            type: "group",
            frame: 0,
            box: textFrame,
            style: {
              alignItems: "stretch",
              display: "flex",
              flexDirection: "column",
              justifyContent: scrollJustification,
              overflow: "hidden",
            },
            children: [
              {
                ...textNode,
                box: undefined,
                style: {
                  ...textNode.style,
                  display: "block",
                  height: undefined,
                  overflow: "visible",
                  width: "100%",
                },
              },
            ],
          }
        : {
            ...textNode,
            box: textFrame,
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

function growingHeight(
  minimumHeight: number,
  paddingY: number,
  lineHeight: number,
  maxLines: number,
  contentHeight: number,
) {
  const maximumContentHeight = Math.max(1, Math.floor(maxLines)) * lineHeight;
  const visibleContentHeight = Math.min(contentHeight, maximumContentHeight);
  return Math.max(1, minimumHeight, visibleContentHeight + paddingY * 2);
}

function textBoxJustify(textAlign: TextBoxDesignContract["textAlign"]) {
  return textAlign === "right"
    ? "flex-end"
    : textAlign === "center" ? "center" : "flex-start";
}
