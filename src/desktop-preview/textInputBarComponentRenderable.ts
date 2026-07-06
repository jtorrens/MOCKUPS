import type { RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  colorForMode,
  numberToken,
  renderScale,
  selectedColor,
  shadow,
  surfaceVisualPadding,
  variants,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
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
  const borderWidth = textInput.surface.borderWidth * scale;
  const surfaceRelief = textInput.surface.reliefEnabled
    ? {
        angleDeg: textInput.surface.reliefAngle,
        extension: textInput.surface.reliefExtent * scale,
        spread: textInput.surface.reliefSpread * scale,
        upperIntensity:
          textInput.surface.reliefTopIntensity * textInput.backgroundAlpha,
        lowerIntensity:
          textInput.surface.reliefBottomIntensity * textInput.backgroundAlpha,
      }
    : undefined;
  const inputShadow = textInput.surface.shadowEnabled ? shadow(payload) : undefined;
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
    role: "text_input_bar",
    frame: 0,
    box: outerBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${textInput.id}.surface`,
        type: "surface",
        role: "text_input_bar_surface",
        frame: 0,
        box: fieldBox,
        style: {
          background: selectedColor(
            payload,
            textInput.backgroundColorToken,
            textInput.backgroundAlpha,
          ),
          borderColor: selectedColor(payload, textInput.surface.borderColorToken),
          borderRadius: numberToken(payload, textInput.surface.cornerRadiusToken) * scale,
          borderWidth,
          shadow: inputShadow,
          surfaceRelief,
          colorModes: Object.fromEntries(
            variants(payload).map((mode) => [
              mode,
              {
                background: colorForMode(
                  payload,
                  textInput.backgroundColorToken,
                  mode,
                  textInput.backgroundAlpha,
                ),
                borderColor: colorForMode(
                  payload,
                  textInput.surface.borderColorToken,
                  mode,
                ),
              },
            ]),
          ),
        },
      },
      {
        id: `${textInput.id}.text`,
        type: "text",
        role: "text_input_bar_text",
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
        role: "text_input_bar_cursor",
        frame: 0,
        box: cursorBox,
        style: {
          background: selectedColor(payload, textInput.cursorColorToken),
          borderRadius: Math.max(1, cursorBox.width / 2),
          opacity: textInput.cursorBlinkFrames > 0 ? 1 : 0,
        },
      },
    ],
  };
}
