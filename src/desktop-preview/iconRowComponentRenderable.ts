import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { ButtonIconDesignContract } from "./buttonIconComponentContract.js";
import {
  boundedCenterBox,
  numberToken,
  renderScale,
} from "./componentRenderableCommon.js";
import { buttonIconComponentToRenderableAt } from "./buttonIconComponentRenderable.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";

export function measureIconRowComponent(
  payload: DesignPreviewPayload,
  iconRow: IconRowDesignContract,
) {
  const scale = renderScale(payload);
  const count = iconRow.buttons.length;
  const buttonSizes = iconRow.buttons.map((button) => iconRowButtonSize(payload, iconRow.sizeToken, button));
  const maxButtonSize = buttonSizes.length > 0 ? Math.max(...buttonSizes) : 0;
  const gap = Math.max(0, numberToken(payload, iconRow.gapToken) * scale);
  const length = count === 0
    ? 0
    : buttonSizes.reduce((sum, value) => sum + value, 0) + Math.max(0, count - 1) * gap;
  return iconRow.orientation === "vertical"
    ? { width: maxButtonSize, height: length, buttonSizes, gap }
    : { width: length, height: maxButtonSize, buttonSizes, gap };
}

export function iconRowComponentToRenderable(
  payload: DesignPreviewPayload,
  iconRow: IconRowDesignContract,
): RenderableNode {
  const size = measureIconRowComponent(payload, iconRow);
  return iconRowComponentToRenderableAt(
    payload,
    iconRow,
    boundedCenterBox(payload, size.width, size.height),
  );
}

export function iconRowComponentToRenderableAt(
  payload: DesignPreviewPayload,
  iconRow: IconRowDesignContract,
  box: RenderableBox,
): RenderableNode {
  const size = measureIconRowComponent(payload, iconRow);
  let cursor = 0;
  const children = iconRow.buttons.map((button, index) => {
    const buttonSize = size.buttonSizes[index] ?? 0;
    const buttonBox = iconRow.orientation === "vertical"
      ? {
          x: box.x + (box.width - buttonSize) * 0.5,
          y: box.y + cursor,
          width: buttonSize,
          height: buttonSize,
        }
      : {
          x: box.x + cursor,
          y: box.y + (box.height - buttonSize) * 0.5,
          width: buttonSize,
          height: buttonSize,
        };
    cursor += buttonSize + size.gap;
    return buttonIconComponentToRenderableAt(payload, button, buttonBox);
  });

  return {
    id: iconRow.id,
    type: "group",
    frame: 0,
    box,
    style: {
      overflow: "visible",
    },
    children,
  };
}

function iconRowButtonSize(
  payload: DesignPreviewPayload,
  sizeToken: string,
  button: ButtonIconDesignContract,
) {
  const scale = renderScale(payload);
  const tokenSize = Math.max(1, numberToken(payload, sizeToken) * scale);
  if (button.sizeMode === "iconSize") {
    return tokenSize + Math.max(0, numberToken(payload, button.iconPaddingToken) * scale) * 2;
  }

  return tokenSize;
}
