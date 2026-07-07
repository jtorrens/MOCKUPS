import type { RenderableNode } from "../visual/renderable/types.js";
import {
  numberToken,
  previewScreenBox,
  renderScale,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  iconRowComponentToRenderableAt,
  measureIconRowComponent,
} from "./iconRowComponentRenderable.js";
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
  const leftIconSize = measureIconRowComponent(payload, textInput.leftIconRow);
  const rightIconSize = measureIconRowComponent(payload, textInput.rightIconRow);
  const hasLeftIcons = textInput.leftIconRow.buttons.length > 0;
  const hasRightIcons = textInput.rightIconRow.buttons.length > 0;
  const iconGap = Math.max(0, numberToken(payload, textInput.iconGapToken) * scale);
  const leftGap = hasLeftIcons ? iconGap : 0;
  const rightGap = hasRightIcons ? iconGap : 0;
  const textBoxWidth = Math.max(
    1,
    width - leftIconSize.width - rightIconSize.width - leftGap - rightGap,
  );
  const measuredTextBox = {
    ...textInput.textBox,
    size: {
      width: Math.max(1, textBoxWidth / scale),
      height: Math.max(1, height / scale),
    },
  };
  const measuredTextBoxSize = measureTextBoxComponent(payload, measuredTextBox);
  const fieldHeight = Math.max(
    height,
    measuredTextBoxSize.height,
    leftIconSize.height,
    rightIconSize.height,
  );
  const barBox = {
    x: screenBox.x,
    y: screenBox.y + (screenBox.height - fieldHeight - barPaddingY * 2) / 2,
    width: screenBox.width,
    height: fieldHeight + barPaddingY * 2,
  };
  const fieldStartX = screenBox.x + barPaddingX;
  const leftIconBox = {
    x: fieldStartX,
    y: barBox.y + barPaddingY + fieldHeight - leftIconSize.height,
    width: leftIconSize.width,
    height: leftIconSize.height,
  };
  const fieldBox = {
    x: fieldStartX + leftIconSize.width + leftGap,
    y: barBox.y + barPaddingY,
    width: textBoxWidth,
    height: fieldHeight,
  };
  const rightIconBox = {
    x: fieldBox.x + fieldBox.width + rightGap,
    y: barBox.y + barPaddingY + fieldHeight - rightIconSize.height,
    width: rightIconSize.width,
    height: rightIconSize.height,
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
      ...(hasLeftIcons
        ? [iconRowComponentToRenderableAt(payload, textInput.leftIconRow, leftIconBox)]
        : []),
      textBoxComponentToRenderableAt(payload, resolvedTextBox, fieldBox),
      ...(hasRightIcons
        ? [iconRowComponentToRenderableAt(payload, textInput.rightIconRow, rightIconBox)]
        : []),
    ],
  };
}
