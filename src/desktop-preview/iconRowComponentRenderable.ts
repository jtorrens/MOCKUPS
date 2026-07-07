import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
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
  const buttonSize = Math.max(1, iconRow.size * scale);
  const gap = Math.max(0, numberToken(payload, iconRow.gapToken) * scale);
  const length = count === 0
    ? 0
    : count * buttonSize + Math.max(0, count - 1) * gap;
  return iconRow.orientation === "vertical"
    ? { width: buttonSize, height: length, buttonSize, gap }
    : { width: length, height: buttonSize, buttonSize, gap };
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
  const children = iconRow.buttons.map((button, index) => {
    const offset = index * (size.buttonSize + size.gap);
    const buttonBox = iconRow.orientation === "vertical"
      ? {
          x: box.x,
          y: box.y + offset,
          width: size.buttonSize,
          height: size.buttonSize,
        }
      : {
          x: box.x + offset,
          y: box.y,
          width: size.buttonSize,
          height: size.buttonSize,
        };
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
