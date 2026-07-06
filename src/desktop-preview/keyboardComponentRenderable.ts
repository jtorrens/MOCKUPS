import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  colorForMode,
  iconTokenStyle,
  numberToken,
  renderScale,
  selectedColor,
  shadow,
  surfaceVisualPadding,
  variants,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { KeyboardDesignContract } from "./keyboardComponentContract.js";

const keyRows = [
  ["Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P"],
  ["A", "S", "D", "F", "G", "H", "J", "K", "L"],
  ["⇧", "Z", "X", "C", "V", "B", "N", "M", "⌫"],
  ["123", "space", "return"],
] as const;

export function keyboardComponentToRenderable(
  payload: DesignPreviewPayload,
  keyboard: KeyboardDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const width = Math.min(payload.previewFrame.screenWidth * 0.94, 640 * scale);
  const height = Math.min(payload.previewFrame.screenHeight * 0.36, 290 * scale);
  const borderWidth = keyboard.surface.borderWidth * scale;
  const keyboardRelief = keyboard.surface.reliefEnabled
    ? {
        angleDeg: keyboard.surface.reliefAngle,
        extension: keyboard.surface.reliefExtent * scale,
        spread: keyboard.surface.reliefSpread * scale,
        upperIntensity: keyboard.surface.reliefTopIntensity * keyboard.backgroundAlpha,
        lowerIntensity: keyboard.surface.reliefBottomIntensity * keyboard.backgroundAlpha,
      }
    : undefined;
  const keyboardShadow = keyboard.surface.shadowEnabled ? shadow(payload) : undefined;
  const visualPadding = surfaceVisualPadding(borderWidth, keyboardShadow, keyboardRelief);
  const outerBox = boundedCenterBox(
    payload,
    width + visualPadding * 2,
    height + visualPadding * 2,
  );
  const keyboardBox = {
    x: outerBox.x + visualPadding,
    y: outerBox.y + visualPadding,
    width,
    height,
  };
  const padding = Math.max(6 * scale, keyboard.keyPadding * scale * 1.5);
  const rowGap = Math.max(5 * scale, keyboard.keyPadding * scale);
  const bottomHeight = Math.max(26 * scale, 32 * scale);
  const rowHeight = Math.max(
    1,
    (height - padding * 2 - bottomHeight - rowGap * keyRows.length) / keyRows.length,
  );
  const keyColor = selectedColor(payload, keyboard.keyTextColorToken);
  const keyBackground = selectedColor(payload, keyboard.keyBackgroundColorToken);
  const bottomIconColor = selectedColor(payload, keyboard.bottomIconColorToken);
  const keyShadow = keyboard.keyShadowEnabled ? shadow(payload) : undefined;

  const keyNodes = keyRows.flatMap((row, rowIndex) =>
    keyboardRowNodes(
      payload,
      keyboard,
      keyboardBox,
      row,
      rowIndex,
      rowHeight,
      padding,
      rowGap,
      keyBackground,
      keyColor,
      keyShadow,
    ),
  );
  const bottomNodes = keyboardBottomIconNodes(
    payload,
    keyboard,
    keyboardBox,
    padding,
    bottomHeight,
    bottomIconColor,
  );

  return {
    id: keyboard.id,
    type: "group",
    role: "keyboard",
    frame: 0,
    box: outerBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${keyboard.id}.surface`,
        type: "surface",
        role: "keyboard_surface",
        frame: 0,
        box: keyboardBox,
        style: {
          background: selectedColor(
            payload,
            keyboard.backgroundColorToken,
            keyboard.backgroundAlpha,
          ),
          borderColor: selectedColor(payload, keyboard.surface.borderColorToken),
          borderRadius: numberToken(payload, keyboard.surface.cornerRadiusToken) * scale,
          borderWidth,
          shadow: keyboardShadow,
          surfaceRelief: keyboardRelief,
          colorModes: Object.fromEntries(
            variants(payload).map((mode) => [
              mode,
              {
                background: colorForMode(
                  payload,
                  keyboard.backgroundColorToken,
                  mode,
                  keyboard.backgroundAlpha,
                ),
                borderColor: colorForMode(
                  payload,
                  keyboard.surface.borderColorToken,
                  mode,
                ),
              },
            ]),
          ),
        },
      },
      ...keyNodes,
      ...bottomNodes,
    ],
    metadata: {
      route: "component-resolver.keyboard-renderable",
      componentType: "keyboard",
    },
  };
}

function keyboardRowNodes(
  payload: DesignPreviewPayload,
  keyboard: KeyboardDesignContract,
  keyboardBox: RenderableBox,
  row: readonly string[],
  rowIndex: number,
  rowHeight: number,
  padding: number,
  rowGap: number,
  keyBackground: string,
  keyColor: string,
  keyShadow: Record<string, unknown> | undefined,
): RenderableNode[] {
  const scale = renderScale(payload);
  const gap = Math.max(3 * scale, keyboard.keyPadding * scale);
  const rowWidth = keyboardBox.width - padding * 2;
  const rowY = keyboardBox.y + padding + rowIndex * (rowHeight + rowGap);
  const weights = row.map((label) =>
    label === "space" ? 5 : label === "return" || label === "123" ? 1.6 : 1,
  );
  const totalWeight = weights.reduce((sum, weight) => sum + weight, 0);
  const keyUnitWidth = Math.max(1, (rowWidth - gap * (row.length - 1)) / totalWeight);
  let x = keyboardBox.x + padding;

  return row.flatMap((label, index) => {
    const width = keyUnitWidth * (weights[index] ?? 1);
    const baseBox = {
      x,
      y: rowY,
      width,
      height: rowHeight,
    };
    x += width + gap;
    const displayText = label === "space" ? "" : label;
    const isSpecial = label.length > 1 || label === "⇧" || label === "⌫";
    const fontScale = label === "😀"
      ? keyboard.emojiScale
      : isSpecial
        ? keyboard.specialKeyTextScale
        : 1;
    const pressedScale = keyboard.pressedEffect === "scale" && rowIndex === 1 && index === 3
      ? 0.94
      : 1;
    const box = pressedScale < 1 ? scaleBox(baseBox, pressedScale) : baseBox;
    return [
      {
        id: `${keyboard.id}.key.${rowIndex}.${index}.surface`,
        type: "surface",
        role: "keyboard_key_surface",
        frame: 0,
        box,
        style: {
          background: keyBackground,
          borderRadius: Math.max(0, keyboard.keyCornerRadius * scale),
          shadow: keyShadow,
        },
      },
      {
        id: `${keyboard.id}.key.${rowIndex}.${index}.text`,
        type: "text",
        role: "keyboard_key_text",
        frame: 0,
        box,
        text: displayText,
        style: {
          color: keyColor,
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          fontSize: Math.max(8, rowHeight * 0.48 * fontScale),
          lineHeight: rowHeight,
          textAlign: "center",
          whiteSpace: "nowrap",
        },
      },
    ];
  });
}

function scaleBox(box: RenderableBox, amount: number): RenderableBox {
  const nextWidth = box.width * amount;
  const nextHeight = box.height * amount;
  return {
    x: box.x + (box.width - nextWidth) / 2,
    y: box.y + (box.height - nextHeight) / 2,
    width: nextWidth,
    height: nextHeight,
  };
}

function keyboardBottomIconNodes(
  payload: DesignPreviewPayload,
  keyboard: KeyboardDesignContract,
  keyboardBox: RenderableBox,
  padding: number,
  bottomHeight: number,
  iconColor: string,
): RenderableNode[] {
  const slots = keyboard.bottomIconSlots;
  const tokens = [...slots.left, ...slots.center, ...slots.right];
  if (tokens.length === 0) return [];

  const scale = renderScale(payload);
  const iconSize = Math.min(bottomHeight * 0.72, 22 * scale);
  const gap = 12 * scale;
  const totalWidth = tokens.length * iconSize + Math.max(0, tokens.length - 1) * gap;
  let x = keyboardBox.x + keyboardBox.width / 2 - totalWidth / 2;
  const y = keyboardBox.y + keyboardBox.height - padding - bottomHeight / 2 - iconSize / 2;
  return tokens.map((token, index) => {
    const node = {
      id: `${keyboard.id}.bottomIcon.${index}`,
      type: "icon_token",
      role: "keyboard_bottom_icon",
      frame: 0,
      box: {
        x,
        y,
        width: iconSize,
        height: iconSize,
      },
      text: token,
      style: {
        ...iconTokenStyle(payload, token, iconColor),
      },
      metadata: {
        token,
      },
    };
    x += iconSize + gap;
    return node;
  });
}
