import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { boundedCenterBox, numberToken, renderScale } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { KeypadDesignContract } from "./keypadComponentContract.js";
import { labelComponentToRenderableAt } from "./labelComponentRenderable.js";

export function keypadComponentToRenderable(
  payload: DesignPreviewPayload,
  keypad: KeypadDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const paddingX = Math.max(0, numberToken(payload, keypad.padding.xToken) * scale);
  const paddingY = Math.max(0, numberToken(payload, keypad.padding.yToken) * scale);
  const columnGap = Math.max(0, numberToken(payload, keypad.columnGapToken) * scale);
  const rowGap = Math.max(0, numberToken(payload, keypad.rowGapToken) * scale);
  const rowCount = Math.ceil(keypad.keys.length / keypad.columns);
  const keyHeight = keypad.keySize.height * scale;
  const naturalWidth = paddingX * 2
    + keypad.keySize.width * scale * keypad.columns
    + columnGap * Math.max(0, keypad.columns - 1);
  const width = keypad.sizingMode === "fill"
    ? Math.max(1, keypad.availableWidth * scale)
    : Math.max(1, naturalWidth);
  const innerWidth = Math.max(1, width - paddingX * 2);
  const keyWidth = keypad.sizingMode === "fill"
    ? Math.max(1, (innerWidth - columnGap * Math.max(0, keypad.columns - 1)) / keypad.columns)
    : keypad.keySize.width * scale;
  const height = Math.max(
    1,
    paddingY * 2 + keyHeight * rowCount + rowGap * Math.max(0, rowCount - 1),
  );
  const box = boundedCenterBox(payload, width, height);
  const children = keypad.keys.flatMap((key, index) => {
    if (!key.label) return [];
    const row = Math.floor(index / keypad.columns);
    const column = index % keypad.columns;
    const keyBox: RenderableBox = {
      x: box.x + paddingX + column * (keyWidth + columnGap),
      y: box.y + paddingY + row * (keyHeight + rowGap),
      width: keyWidth,
      height: keyHeight,
    };
    return [labelComponentToRenderableAt(payload, key.label, keyBox)];
  });
  return {
    id: keypad.id,
    type: "group",
    frame: 0,
    box,
    style: { overflow: "visible" },
    children,
  };
}
