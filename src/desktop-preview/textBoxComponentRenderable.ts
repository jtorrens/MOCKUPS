import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  centerBox,
  numberToken,
  renderScale,
  selectedColor,
  shadow,
  surfaceVisualPadding,
} from "./componentRenderableCommon.js";
import { cursorComponentToRenderableAt } from "./cursorComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  approximateTextWidth,
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
  const paddingX = numberToken(payload, textBox.padding.xToken) * scale;
  const paddingY = numberToken(payload, textBox.padding.yToken) * scale;
  const contentText = visibleText(textBox);
  if (textBox.dimensionMode === "fixed") {
    return {
      width: textBox.size.width * scale,
      height: textBox.size.height * scale,
      typography,
      paddingX,
      paddingY,
      contentText,
    };
  }

  return {
    width: Math.max(1, approximateTextWidth(contentText, typography.fontSize) + paddingX * 2),
    height: Math.max(1, typography.lineHeight + paddingY * 2),
    typography,
    paddingX,
    paddingY,
    contentText,
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
  const textFrame = {
    x: box.x + size.paddingX,
    y: box.y + size.paddingY,
    width: Math.max(1, box.width - size.paddingX * 2),
    height: Math.max(1, box.height - size.paddingY * 2),
  };
  const textIsEmpty = textBox.text.trim().length === 0;
  const cursorWidth = Math.max(1, textBox.cursor.width * scale);
  const cursorHeight = Math.max(1, size.typography.fontSize);
  const cursorX = cursorXForText(textBox.textAlign, textFrame, textBox.text, size.typography.fontSize, cursorWidth);
  const cursorBox = {
    x: cursorX,
    y: textFrame.y + (textFrame.height - cursorHeight) / 2,
    width: cursorWidth,
    height: cursorHeight,
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
      {
        id: `${textBox.id}.text`,
        type: "text",
        frame: 0,
        box: textFrame,
        text: size.contentText,
        style: {
          textColor: selectedColor(
            payload,
            textIsEmpty ? textBox.placeholderColorToken : textBox.textColorToken,
          ),
          display: "flex",
          alignItems: "center",
          fontSize: size.typography.fontSize,
          fontStyle: size.typography.fontStyle,
          fontWeight: size.typography.fontWeight,
          justifyContent: textBoxJustify(textBox.textAlign),
          lineHeight: size.typography.lineHeight,
          overflow: textBox.overflowMode === "scroll" ? "auto" : "hidden",
          textAlign: textBox.textAlign,
          whiteSpace: "nowrap",
        },
      },
      ...(textBox.cursorVisible && !textIsEmpty
        ? [cursorComponentToRenderableAt(payload, textBox.cursor, cursorBox)]
        : []),
    ],
  };
}

function visibleText(textBox: TextBoxDesignContract) {
  return textBox.text.trim().length > 0 ? textBox.text : textBox.placeholder;
}

function textBoxJustify(textAlign: TextBoxDesignContract["textAlign"]) {
  return textAlign === "right"
    ? "flex-end"
    : textAlign === "center" ? "center" : "flex-start";
}

function cursorXForText(
  textAlign: TextBoxDesignContract["textAlign"],
  box: RenderableBox,
  text: string,
  fontSize: number,
  cursorWidth: number,
) {
  const textWidth = Math.min(box.width, approximateTextWidth(text, fontSize));
  if (textAlign === "right") {
    return Math.max(box.x, box.x + box.width - cursorWidth);
  }
  if (textAlign === "center") {
    return Math.max(
      box.x,
      Math.min(box.x + box.width - cursorWidth, box.x + (box.width + textWidth) / 2),
    );
  }
  return Math.max(box.x, Math.min(box.x + box.width - cursorWidth, box.x + textWidth));
}
