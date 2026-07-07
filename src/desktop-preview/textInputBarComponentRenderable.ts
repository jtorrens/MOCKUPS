import type { RenderableNode } from "../visual/renderable/types.js";
import {
  numberToken,
  previewScreenBox,
  renderScale,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import {
  measureTextBoxComponent,
  textBoxComponentToRenderableAt,
} from "./textBoxComponentRenderable.js";
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
  const width = Math.max(1, screenBox.width - barPaddingX * 2);
  const measuredTextBox = {
    ...textInput.textBox,
    size: {
      width: Math.max(1, width / scale),
      height: Math.max(1, height / scale),
    },
  };
  const measuredTextBoxSize = measureTextBoxComponent(payload, measuredTextBox);
  const fieldHeight = Math.max(height, measuredTextBoxSize.height);
  const barBox = {
    x: screenBox.x,
    y: screenBox.y + (screenBox.height - fieldHeight - barPaddingY * 2) / 2,
    width: screenBox.width,
    height: fieldHeight + barPaddingY * 2,
  };
  const fieldBox = {
    x: screenBox.x + barPaddingX,
    y: barBox.y + barPaddingY,
    width,
    height: fieldHeight,
  };
  const resolvedTextBox = {
    ...textInput.textBox,
    size: {
      width: Math.max(1, fieldBox.width / scale),
      height: Math.max(1, fieldBox.height / scale),
    },
  };

  return {
    id: textInput.id,
    type: "group",
    frame: 0,
    box: barBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        ...surfaceComponentToRenderableAt(payload, textInput.barSurface, barBox),
        id: `${textInput.id}.barSurface`,
      },
      textBoxComponentToRenderableAt(payload, resolvedTextBox, fieldBox),
    ],
  };
}
