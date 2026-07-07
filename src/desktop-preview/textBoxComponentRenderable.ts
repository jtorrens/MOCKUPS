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
    const paddingX = basePaddingX + effectiveCornerInset(cornerRadius, width, height);
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

  const height = Math.max(1, contentSize.height + paddingY * 2);
  const paddingX = basePaddingX + Math.min(cornerRadius, height * 0.5);
  return {
    width: Math.max(1, contentSize.width + paddingX * 2),
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
  const paddingX = size.basePaddingX + effectiveCornerInset(size.cornerRadius, box.width, box.height);
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
  const textContentHeight = Math.max(textFrame.height, wrappedContentSize.height);
  const scrollOffset = textBox.overflowMode === "scroll"
    ? Math.max(0, textContentHeight - textFrame.height)
    : 0;
  const textNode: RenderableNode = {
    id: `${textBox.id}.text`,
    type: "text",
    frame: 0,
    box: {
      x: textFrame.x,
      y: textFrame.y - scrollOffset,
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
      overflow: textBox.overflowMode === "scroll" ? "visible" : "hidden",
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
              overflow: "hidden",
            },
            children: [textNode],
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

function effectiveCornerInset(cornerRadius: number, width: number, height: number) {
  return Math.min(
    Math.max(0, cornerRadius),
    Math.max(0, width) * 0.5,
    Math.max(0, height) * 0.5,
  );
}

function textBoxJustify(textAlign: TextBoxDesignContract["textAlign"]) {
  return textAlign === "right"
    ? "flex-end"
    : textAlign === "center" ? "center" : "flex-start";
}
