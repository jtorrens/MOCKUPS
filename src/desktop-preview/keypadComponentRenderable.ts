import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  colorForMode,
  iconTokenStyle,
  numberToken,
  renderScale,
  selectedColor,
  variants,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { KeypadDesignContract } from "./keypadComponentContract.js";
import { labelComponentToRenderableAt } from "./labelComponentRenderable.js";

export function keypadComponentToRenderable(
  payload: DesignPreviewPayload,
  keypad: KeypadDesignContract,
): RenderableNode {
  const size = measureKeypadComponent(payload, keypad);
  return keypadComponentToRenderableAt(
    payload,
    keypad,
    boundedCenterBox(payload, size.width, size.height),
  );
}

export function measureKeypadComponent(
  payload: DesignPreviewPayload,
  keypad: KeypadDesignContract,
) {
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
  const height = Math.max(
    1,
    paddingY * 2 + keyHeight * rowCount + rowGap * Math.max(0, rowCount - 1),
  );
  return { width, height };
}

export function keypadComponentToRenderableAt(
  payload: DesignPreviewPayload,
  keypad: KeypadDesignContract,
  box: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const paddingX = Math.max(0, numberToken(payload, keypad.padding.xToken) * scale);
  const paddingY = Math.max(0, numberToken(payload, keypad.padding.yToken) * scale);
  const columnGap = Math.max(0, numberToken(payload, keypad.columnGapToken) * scale);
  const rowGap = Math.max(0, numberToken(payload, keypad.rowGapToken) * scale);
  const keyHeight = keypad.keySize.height * scale;
  const innerWidth = Math.max(1, box.width - paddingX * 2);
  const keyWidth = keypad.sizingMode === "fill"
    ? Math.max(1, (innerWidth - columnGap * Math.max(0, keypad.columns - 1)) / keypad.columns)
    : keypad.keySize.width * scale;
  const children = keypad.keys.flatMap((key, index) => {
    if (!key.label
      || !key.backgroundColorToken
      || !key.textColorToken
      || key.backgroundAlpha === undefined
      || key.borderAlpha === undefined) return [];
    const row = Math.floor(index / keypad.columns);
    const column = index % keypad.columns;
    const keyBox: RenderableBox = {
      x: box.x + paddingX + column * (keyWidth + columnGap),
      y: box.y + paddingY + row * (keyHeight + rowGap),
      width: keyWidth,
      height: keyHeight,
    };
    const textColor = selectedColor(payload, key.textColorToken);
    const keyChildren: RenderableNode[] = [labelComponentToRenderableAt(
      payload,
      key.label,
      keyBox,
      {
        textColor,
        subtextColor: textColor,
        surfaceColors: {
          background: selectedColor(payload, key.backgroundColorToken, key.backgroundAlpha),
          borderColor: selectedColor(
            payload,
            key.label.surface.surface.borderColorToken,
            key.borderAlpha,
          ),
          colorModes: Object.fromEntries(
            variants(payload).map((mode) => [
              mode,
              {
                background: colorForMode(
                  payload,
                  key.backgroundColorToken!,
                  mode,
                  key.backgroundAlpha!,
                ),
                borderColor: colorForMode(
                  payload,
                  key.label!.surface.surface.borderColorToken,
                  mode,
                  key.borderAlpha!,
                ),
              },
            ]),
          ),
        },
      },
    )];
    if (key.kind === "icon") {
      const iconSize = Math.max(1, numberToken(payload, keypad.iconSizeToken) * scale);
      keyChildren.push({
        id: `${key.label.id}.icon`,
        type: "icon",
        frame: 0,
        box: {
          x: keyBox.x + (keyBox.width - iconSize) * 0.5,
          y: keyBox.y + (keyBox.height - iconSize) * 0.5,
          width: iconSize,
          height: iconSize,
        },
        text: key.iconToken,
        style: iconTokenStyle(
          payload,
          key.iconToken,
          textColor,
        ),
      });
    }
    return keyChildren;
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
