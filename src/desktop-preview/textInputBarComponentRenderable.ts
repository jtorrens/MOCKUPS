import type { RenderableNode } from "../visual/renderable/types.js";
import {
  numberToken,
  previewScreenBox,
  renderScale,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  iconBarComponentToRenderableAt,
  measureIconBarZoneComponent,
} from "./iconBarComponentRenderable.js";
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
  const leftIconRow = textInput.iconBar.rows.left;
  const rightIconRow = textInput.iconBar.rows.right;
  const centerIconRow = textInput.iconBar.rows.center;
  const leftIconSize = measureIconBarZoneComponent(payload, textInput.iconBar, "left");
  const rightIconSize = measureIconBarZoneComponent(payload, textInput.iconBar, "right");
  const centerIconSize = measureIconBarZoneComponent(payload, textInput.iconBar, "center");
  const hasLeftIcons = leftIconRow.buttons.length > 0;
  const hasRightIcons = rightIconRow.buttons.length > 0;
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
  const textLineCount = Math.max(
    1,
    Math.ceil(measuredTextBoxSize.contentTextHeight / measuredTextBoxSize.typography.lineHeight),
  );
  const iconY = (iconHeight: number) =>
    textLineCount <= 1
      ? barBox.y + barPaddingY + Math.max(0, (fieldHeight - iconHeight) * 0.5)
      : barBox.y + barPaddingY + fieldHeight - iconHeight;
  const fieldStartX = screenBox.x + barPaddingX;
  const fieldBox = {
    x: fieldStartX + leftIconSize.width + leftGap,
    y: barBox.y + barPaddingY,
    width: textBoxWidth,
    height: fieldHeight,
  };
  const iconBarHeight = Math.max(
    leftIconSize.height,
    centerIconSize.height,
    rightIconSize.height,
  );
  const iconBarBox = {
    x: fieldStartX,
    y: iconY(iconBarHeight),
    width,
    height: iconBarHeight,
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
      ...(iconBarHeight > 0
        ? [iconBarComponentToRenderableAt(payload, textInput.iconBar, iconBarBox)]
        : []),
    ],
  };
}
