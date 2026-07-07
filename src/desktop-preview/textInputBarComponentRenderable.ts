import type { RenderableNode } from "../visual/renderable/types.js";
import {
  numberToken,
  previewScreenBox,
  renderScale,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import { textBoxComponentToRenderableAt } from "./textBoxComponentRenderable.js";
import {
  iconRowComponentToRenderableAt,
  measureIconRowComponent,
} from "./iconRowComponentRenderable.js";
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
  const iconGap = Math.max(0, numberToken(payload, textInput.iconGapToken) * scale);
  const leftIconSize = measureIconRowComponent(payload, textInput.leftIconRow);
  const rightIconSize = measureIconRowComponent(payload, textInput.rightIconRow);
  const hasLeftIcons = textInput.leftIconRow.icons.length > 0;
  const hasRightIcons = textInput.rightIconRow.icons.length > 0;
  const barBox = {
    x: screenBox.x,
    y: screenBox.y + (screenBox.height - height - barPaddingY * 2) / 2,
    width: screenBox.width,
    height: height + barPaddingY * 2,
  };
  const width = Math.max(1, screenBox.width - barPaddingX * 2);
  const fieldBox = {
    x: screenBox.x + barPaddingX + (hasLeftIcons ? leftIconSize.width + iconGap : 0),
    y: barBox.y + barPaddingY,
    width: Math.max(
      1,
      width
        - (hasLeftIcons ? leftIconSize.width + iconGap : 0)
        - (hasRightIcons ? rightIconSize.width + iconGap : 0),
    ),
    height,
  };
  const leftIconBox = {
    x: screenBox.x + barPaddingX,
    y: fieldBox.y + Math.max(0, (height - leftIconSize.height) * 0.5),
    width: leftIconSize.width,
    height: leftIconSize.height,
  };
  const rightIconBox = {
    x: screenBox.x + screenBox.width - barPaddingX - rightIconSize.width,
    y: fieldBox.y + Math.max(0, (height - rightIconSize.height) * 0.5),
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
      textBoxComponentToRenderableAt(payload, resolvedTextBox, fieldBox),
      ...(hasLeftIcons
        ? [iconRowComponentToRenderableAt(payload, textInput.leftIconRow, leftIconBox)]
        : []),
      ...(hasRightIcons
        ? [iconRowComponentToRenderableAt(payload, textInput.rightIconRow, rightIconBox)]
        : []),
    ],
  };
}
