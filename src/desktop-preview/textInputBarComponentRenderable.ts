import type { RenderableNode } from "../visual/renderable/types.js";
import {
  numberToken,
  previewScreenBox,
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
  const screenBox = previewScreenBox(payload);
  const barPaddingX = Math.max(0, numberToken(payload, textInput.barPadding.xToken) * scale);
  const barPaddingY = Math.max(0, numberToken(payload, textInput.barPadding.yToken) * scale);
  const textPaddingX = Math.max(0, numberToken(payload, textInput.textPadding.xToken) * scale);
  const textPaddingY = Math.max(0, numberToken(payload, textInput.textPadding.yToken) * scale);
  const barBox = {
    x: screenBox.x,
    y: screenBox.y + (screenBox.height - height - barPaddingY * 2) / 2,
    width: screenBox.width,
    height: height + barPaddingY * 2,
  };
  const width = Math.max(1, screenBox.width - barPaddingX * 2);
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
  const outerBox = {
    x: screenBox.x,
    y: barBox.y - visualPadding,
    width: screenBox.width,
    height: barBox.height + visualPadding * 2,
  };
  const fieldBox = {
    x: screenBox.x + barPaddingX,
    y: barBox.y + barPaddingY,
    width,
    height,
  };
  const textBox = {
    x: fieldBox.x + textPaddingX,
    y: fieldBox.y + textPaddingY,
    width: Math.max(1, fieldBox.width - textPaddingX * 2),
    height: Math.max(1, fieldBox.height - textPaddingY * 2),
  };
  const textValue = textInput.text.trim().length > 0
    ? textInput.text
    : textInput.placeholder;
  const cursorWidth = Math.max(1, textInput.cursorWidth * scale);

  return {
    id: textInput.id,
    type: "group",
    frame: 0,
    box: outerBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        ...surfaceComponentToRenderableAt(payload, textInput.surface, fieldBox),
        id: `${textInput.id}.surface`,
      },
      {
        id: `${textInput.id}.text`,
        type: "text",
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
        metadata: textInput.text.trim().length > 0 && textInput.cursorBlinkFrames > 0
          ? {
              inlineCursor: {
                color: selectedColor(payload, textInput.cursorColorToken),
                width: cursorWidth,
                opacity: 1,
              },
            }
          : undefined,
      },
    ],
  };
}
