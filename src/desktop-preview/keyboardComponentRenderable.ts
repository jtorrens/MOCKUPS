import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  colorForMode,
  iconTokenStyle,
  numberToken,
  previewScreenBox,
  renderScale,
  selectedColor,
  shadow,
  surfaceVisualPadding,
  variants,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { resolveTypographyStyle } from "./previewTextHelpers.js";
import { iconBarComponentToRenderableAt } from "./iconBarComponentRenderable.js";
import type {
  KeyboardDesignContract,
  KeyboardKeyContract,
} from "./keyboardComponentContract.js";
import { wrapMotionFrame } from "./previewMotionHelpers.js";

export function keyboardComponentToRenderable(
  payload: DesignPreviewPayload,
  keyboard: KeyboardDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const width = payload.previewFrame.screenWidth;
  const height = Math.max(1, numberToken(payload, keyboard.heightToken) * scale);
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
  const outerWidth = width + visualPadding * 2;
  const outerHeight = height + visualPadding * 2;
  const outerBox = {
    x: payload.previewFrame.screenX + (payload.previewFrame.screenWidth - outerWidth) / 2,
    y: payload.previewFrame.screenY + payload.previewFrame.screenHeight - outerHeight,
    width: outerWidth,
    height: outerHeight,
  };
  const keyboardBox = {
    x: outerBox.x + visualPadding,
    y: outerBox.y + visualPadding,
    width,
    height,
  };

  const keyPadding = numberToken(payload, keyboard.keyPaddingToken) * scale;
  const padding = Math.max(6 * scale, keyPadding);
  const keyGap = Math.max(0, numberToken(payload, keyboard.keyGapToken) * scale);
  const rowGap = Math.max(0, numberToken(payload, keyboard.rowGapToken) * scale);
  const iconRowsHeight = Math.max(keyboard.iconRowsHeight * scale, 0);
  const rowCount = Math.max(1, keyboard.rows.length);
  const rowHeight = Math.max(
    1,
    (height - padding * 2 - iconRowsHeight - rowGap * (rowCount - 1)) / rowCount,
  );
  const keyColor = selectedColor(payload, keyboard.keyTextColorToken);
  const keyBackground = selectedColor(payload, keyboard.keyBackgroundColorToken);
  const specialKeyBackground = selectedColor(
    payload,
    keyboard.specialKeyBackgroundColorToken,
  );
  const pressedKeyBackground = selectedColor(
    payload,
    keyboard.pressedKeyBackgroundColorToken,
  );
  const keyBorderColor = selectedColor(payload, keyboard.keyBorderColorToken);
  const keyBorderWidth = Math.max(0, keyboard.keyBorderWidth * scale);
  const keyShadow = keyboard.keyShadowEnabled ? shadow(payload) : undefined;
  const typography = resolveTypographyStyle(payload, keyboard.typography, scale);
  const rowsStartY = keyboardBox.y
    + padding
    + (keyboard.iconRowPlacement === "top" ? iconRowsHeight : 0);

  const keyNodes = keyboard.rows.flatMap((row, rowIndex) =>
    keyboardRowNodes(
      payload,
      keyboard,
      keyboardBox,
      row,
      rowIndex,
      rowHeight,
      rowsStartY,
      padding,
      keyGap,
      rowGap,
      keyBackground,
      specialKeyBackground,
      pressedKeyBackground,
      keyBorderColor,
      keyBorderWidth,
      keyColor,
      keyShadow,
      typography,
    ),
  );

  const node: RenderableNode = {
    id: keyboard.id,
    type: "group",
    frame: 0,
    box: outerBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${keyboard.id}.background`,
        type: "group",
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
      ...keyboardIconBarNodes(
        payload,
        keyboard,
        keyboardBox,
        iconRowsHeight,
      ),
    ],
  };
  const motionBounds = keyboard.motion.bounds === "screen"
    ? previewScreenBox(payload)
    : outerBox;
  return wrapMotionFrame(
    payload,
    node,
    keyboard.motion,
    keyboard.motionFrame,
    outerBox,
    motionBounds,
  );
}

function keyboardRowNodes(
  payload: DesignPreviewPayload,
  keyboard: KeyboardDesignContract,
  keyboardBox: RenderableBox,
  row: readonly KeyboardKeyContract[],
  rowIndex: number,
  rowHeight: number,
  rowsStartY: number,
  padding: number,
  keyGap: number,
  rowGap: number,
  keyBackground: string,
  specialKeyBackground: string,
  pressedKeyBackground: string,
  keyBorderColor: string,
  keyBorderWidth: number,
  keyColor: string,
  keyShadow: Record<string, unknown> | undefined,
  typography: ReturnType<typeof resolveTypographyStyle>,
): RenderableNode[] {
  const scale = renderScale(payload);
  const rowWidth = keyboardBox.width - padding * 2;
  const rowY = rowsStartY + rowIndex * (rowHeight + rowGap);
  const weights = row.map((key) => key.weight);
  const totalWeight = weights.reduce((sum, weight) => sum + weight, 0);
  const keyUnitWidth = Math.max(1, (rowWidth - keyGap * (row.length - 1)) / totalWeight);
  let x = keyboardBox.x + padding;

  return row.flatMap((key, index) => {
    const width = keyUnitWidth * (weights[index] ?? 1);
    const baseBox = {
      x,
      y: rowY,
      width,
      height: rowHeight,
    };
    x += width + keyGap;

    const displayText = key.kind === "space" ? "" : key.label;
    const usesEmojiIcon = key.id === "emoji";
    const isEmojiKey = key.kind === "emoji"
      || (key.kind === "character" && /\p{Extended_Pictographic}/u.test(key.label));
    const isSpecial = key.kind !== "character" && key.kind !== "space";
    const pressed = isPressedKey(key, keyboard.pressedKey);
    const fontScale = isEmojiKey
      ? keyboard.emojiScale
      : isSpecial
        ? keyboard.specialKeyTextScale
        : 1;
    const pressedScale = keyboard.pressedEffect === "scale" && pressed ? 0.94 : 1;
    const visualBaseBox = isCompactKeyboardKey(key) ? compactKeyBox(baseBox) : baseBox;
    const box = pressedScale < 1 ? scaleBox(visualBaseBox, pressedScale) : visualBaseBox;
    const background = pressed && keyboard.pressedEffect !== "popup"
      ? pressedKeyBackground
      : isSpecial
        ? specialKeyBackground
        : keyBackground;
    const compactGlyphSize = isCompactKeyboardKey(key)
      ? Math.min(box.width, box.height) * 0.52
      : 0;
    const fontSize = Math.max(8, typography.fontSize * fontScale, compactGlyphSize);
    const keyNodes: RenderableNode[] = [
      {
        id: `${keyboard.id}.key.${rowIndex}.${index}.background`,
        type: "group",
        frame: 0,
        box,
        style: {
          background,
          borderColor: keyBorderColor,
          borderRadius: Math.max(0, numberToken(payload, keyboard.keyCornerRadiusToken) * scale),
          borderWidth: keyBorderWidth,
          shadow: isSpecial ? undefined : keyShadow,
        },
      },
      usesEmojiIcon
        ? {
            id: `${keyboard.id}.key.${rowIndex}.${index}.icon`,
            type: "icon",
            frame: 0,
            box: centeredSquareBox(box, Math.min(box.width, box.height) * 0.48),
            text: "system_emoji",
            style: iconTokenStyle(payload, "system_emoji", keyColor),
          }
        : {
        id: `${keyboard.id}.key.${rowIndex}.${index}.text`,
        type: "text",
        frame: 0,
        box,
        text: displayText,
        style: {
          alignItems: "center",
          color: keyColor,
          display: "flex",
          fontFamily: typography.fontFamily,
          fontSize,
          fontStyle: typography.fontStyle,
          fontWeight: isSpecial || isEmojiKey ? 420 : typography.fontWeight,
          justifyContent: "center",
          lineHeight: box.height,
          textAlign: "center",
          whiteSpace: "nowrap",
        },
      },
    ];

    if (pressed && keyboard.pressedEffect === "popup" && displayText) {
      keyNodes.push(...keyboardPopoverNodes(
        keyboard,
        `${rowIndex}.${index}`,
        box,
        displayText,
        background,
        keyColor,
        fontSize * 1.2,
        typography,
        keyShadow,
        Math.max(0, numberToken(payload, keyboard.keyCornerRadiusToken) * scale),
      ));
    }

    return keyNodes;
  });
}

function isPressedKey(key: KeyboardKeyContract, pressedKey: string) {
  return pressedKey.length > 0 && (pressedKey === key.id || pressedKey === key.label);
}

function isCompactKeyboardKey(key: KeyboardKeyContract) {
  return key.id === "shift"
    || key.id === "backspace"
    || key.id === "123"
    || key.id === "numeric"
    || key.id === "emoji";
}

function compactKeyBox(box: RenderableBox): RenderableBox {
  const nextHeight = box.height * 0.74;
  return {
    ...box,
    y: box.y + (box.height - nextHeight) / 2,
    height: nextHeight,
  };
}

function centeredSquareBox(box: RenderableBox, size: number): RenderableBox {
  return {
    x: box.x + (box.width - size) / 2,
    y: box.y + (box.height - size) / 2,
    width: size,
    height: size,
  };
}

function keyboardPopoverNodes(
  keyboard: KeyboardDesignContract,
  idSuffix: string,
  keyBox: RenderableBox,
  text: string,
  background: string,
  color: string,
  fontSize: number,
  typography: ReturnType<typeof resolveTypographyStyle>,
  keyShadow: Record<string, unknown> | undefined,
  radius: number,
): RenderableNode[] {
  const popoverWidth = keyBox.width;
  const popoverHeight = keyBox.height;
  const gap = keyBox.height * 0.18;
  const box = {
    x: keyBox.x + keyBox.width / 2 - popoverWidth / 2,
    y: keyBox.y - popoverHeight - gap,
    width: popoverWidth,
    height: popoverHeight,
  };
  const tailWidth = Math.min(keyBox.width * 0.72, popoverWidth * 0.52);
  const tailHeight = keyBox.height * 0.44;
  const tailBox = {
    x: keyBox.x + keyBox.width / 2 - tailWidth / 2,
    y: box.y + box.height - 1,
    width: tailWidth,
    height: tailHeight,
  };

  return [
    {
      id: `${keyboard.id}.key.${idSuffix}.popover`,
      type: "group",
      frame: 0,
      box,
      style: {
        alignItems: "center",
        background,
        borderRadius: radius,
        shadow: keyShadow,
        color,
        display: "flex",
        justifyContent: "center",
        overflow: "visible",
        zIndex: 20,
      },
      children: [
        {
          id: `${keyboard.id}.key.${idSuffix}.popover.text`,
          type: "text",
          frame: 0,
          box,
          text,
          style: {
            alignItems: "center",
            color,
            display: "flex",
            fontFamily: typography.fontFamily,
            fontSize,
            fontStyle: typography.fontStyle,
            fontWeight: typography.fontWeight,
            justifyContent: "center",
            lineHeight: popoverHeight,
            textAlign: "center",
            whiteSpace: "nowrap",
          },
        },
      ],
    },
    {
      id: `${keyboard.id}.key.${idSuffix}.popover.tail`,
      type: "path",
      frame: 0,
      box: tailBox,
      style: {
        fill: background,
        pathData: "M0 0 H100 V48 Q50 100 0 48 Z",
        preserveAspectRatio: "none",
        viewBox: "0 0 100 100",
        zIndex: 19,
      },
    },
  ];
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

function keyboardIconBarNodes(
  payload: DesignPreviewPayload,
  keyboard: KeyboardDesignContract,
  keyboardBox: RenderableBox,
  iconRowsHeight: number,
): RenderableNode[] {
  if (iconRowsHeight <= 0) return [];

  const scale = renderScale(payload);
  const edgePadding = Math.max(0, numberToken(payload, keyboard.iconEdgePaddingToken) * scale);
  const y = keyboard.iconRowPlacement === "top"
    ? keyboardBox.y
    : keyboardBox.y + keyboardBox.height - iconRowsHeight;
  return [
    iconBarComponentToRenderableAt(payload, keyboard.iconBar, {
      x: keyboardBox.x + edgePadding,
      y,
      width: Math.max(1, keyboardBox.width - edgePadding * 2),
      height: iconRowsHeight,
    }),
  ];
}
