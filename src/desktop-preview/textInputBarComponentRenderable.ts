import type { RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  numberToken,
  renderScale,
  selectedColor,
  shadow,
  surfaceVisualPadding,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import type { TextInputBarDesignContract } from "./textInputBarComponentContract.js";

export function textInputBarComponentToRenderable(
  payload: DesignPreviewPayload,
  textInput: TextInputBarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const height = Math.max(1, textInput.height * scale);
  const width = Math.min(
    payload.previewFrame.screenWidth * 0.82,
    Math.max(240 * scale, 520 * scale),
  );
  const fontSize = numberToken(payload, textInput.textSizeToken) * scale;
  const borderWidth = textInput.surface.surface.borderWidth * scale;
  const surfaceRelief = textInput.surface.surface.reliefEnabled
    ? {
        angleDeg: textInput.surface.surface.reliefAngle,
        extension: textInput.surface.surface.reliefExtent * scale,
        spread: textInput.surface.surface.reliefSpread * scale,
        upperIntensity:
          textInput.surface.surface.reliefTopIntensity *
          textInput.surface.backgroundAlpha,
        lowerIntensity:
          textInput.surface.surface.reliefBottomIntensity *
          textInput.surface.backgroundAlpha,
      }
    : undefined;
  const inputShadow = textInput.surface.surface.shadowEnabled
    ? shadow(payload)
    : undefined;
  const visualPadding = surfaceVisualPadding(borderWidth, inputShadow, surfaceRelief);
  const outerBox = boundedCenterBox(
    payload,
    width + visualPadding * 2,
    height + visualPadding * 2,
  );
  const fieldBox = {
    x: outerBox.x + visualPadding,
    y: outerBox.y + visualPadding,
    width,
    height,
  };
  const horizontalPadding = Math.max(12 * scale, height * 0.28);
  const textBox = {
    x: fieldBox.x + horizontalPadding,
    y: fieldBox.y,
    width: Math.max(1, fieldBox.width - horizontalPadding * 2),
    height: fieldBox.height,
  };
  const textValue = textInput.text.trim().length > 0
    ? textInput.text
    : textInput.placeholder;
  const cursorHeight = Math.max(1, fontSize * 1.15);
  const cursorWidth = Math.max(1, textInput.cursorWidth * scale);
  const cursorOffset = Math.max(
    0,
    Math.min(textBox.width - cursorWidth, textValue.length * fontSize * 0.55),
  );
  const cursorBox = {
    x: textBox.x + cursorOffset,
    y: textBox.y + (textBox.height - cursorHeight) / 2,
    width: cursorWidth,
    height: cursorHeight,
  };

  return {
    id: textInput.id,
    type: "group",
    role: "textInputBar",
    frame: 0,
    box: outerBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        ...surfaceComponentToRenderableAt(payload, textInput.surface, fieldBox),
        id: `${textInput.id}.surface`,
        role: "textInputBarSurface",
      },
      {
        id: `${textInput.id}.text`,
        type: "text",
        role: "textInputBarText",
        frame: 0,
        box: textBox,
        text: textValue,
        style: {
          color: selectedColor(payload, textInput.idleTextColorToken),
          display: "flex",
          alignItems: "center",
          fontSize,
          lineHeight: textBox.height,
          overflow: "hidden",
          whiteSpace: "nowrap",
        },
      },
      {
        id: `${textInput.id}.cursor`,
        type: "surface",
        role: "textInputBarCursor",
        frame: 0,
        box: cursorBox,
        style: {
          background: selectedColor(payload, textInput.cursorColorToken),
          borderRadius: Math.max(1, cursorBox.width / 2),
          opacity:
            textInput.text.trim().length > 0 && textInput.cursorBlinkFrames > 0
              ? 1
              : 0,
        },
      },
    ],
  };
}
