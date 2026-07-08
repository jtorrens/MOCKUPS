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
import {
  iconRowComponentToRenderableAt,
  measureIconRowComponent,
} from "./iconRowComponentRenderable.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
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
  const rowGap = 8 * scale;
  const iconRows = [keyboard.leftIconRow, keyboard.centerIconRow, keyboard.rightIconRow];
  const hasIconRows = iconRows.some((row) => row.buttons.length > 0);
  const iconRowsHeight = hasIconRows
    ? Math.max(keyboard.iconRowsHeight * scale, 1)
    : 0;
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
      ...keyboardIconRowNodes(
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
  const gap = 6 * scale;
  const rowWidth = keyboardBox.width - padding * 2;
  const rowY = rowsStartY + rowIndex * (rowHeight + rowGap);
  const weights = row.map((key) => key.weight);
  const totalWeight = weights.reduce((sum, weight) => sum + weight, 0);
  const keyUnitWidth = Math.max(1, (rowWidth - gap * (row.length - 1)) / totalWeight);
  let x = keyboardBox.x + padding;

  return row.flatMap((key, index) => {
    const width = keyUnitWidth * (weights[index] ?? 1);
    const baseBox = {
      x,
      y: rowY,
      width,
      height: rowHeight,
    };
    x += width + gap;

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
          shadow: keyShadow,
        },
      },
      usesEmojiIcon
        ? {
            id: `${keyboard.id}.key.${rowIndex}.${index}.icon`,
            type: "icon",
            frame: 0,
            box: centeredSquareBox(box, Math.min(box.width, box.height) * 0.48),
            text: "chat_emoji",
            style: iconTokenStyle(payload, "chat_emoji", keyColor),
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
): RenderableNode[] {
  const popoverWidth = keyBox.width;
  const popoverHeight = keyBox.height;
  const gap = keyBox.height * 0.18;
  const radius = Math.max(4, keyBox.height * 0.18);
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
        boxShadow: "0 0.16em 0.34em rgba(0, 0, 0, 0.26)",
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

function keyboardIconRowNodes(
  payload: DesignPreviewPayload,
  keyboard: KeyboardDesignContract,
  keyboardBox: RenderableBox,
  iconRowsHeight: number,
): RenderableNode[] {
  if (iconRowsHeight <= 0) return [];

  const scale = renderScale(payload);
  const edgePadding = Math.max(0, numberToken(payload, keyboard.iconRowsEdgePaddingToken) * scale);
  const verticalPadding = Math.min(edgePadding, Math.max(0, iconRowsHeight / 2 - 1));
  const rowAreaHeight = Math.max(1, iconRowsHeight - verticalPadding * 2);
  const y = keyboard.iconRowPlacement === "top"
    ? keyboardBox.y + verticalPadding
    : keyboardBox.y + keyboardBox.height - iconRowsHeight + verticalPadding;
  return [
    iconRowZoneNode(
      payload,
      keyboard.leftIconRow,
      "left",
      keyboardBox.x + edgePadding,
      y,
      rowAreaHeight,
    ),
    iconRowZoneNode(
      payload,
      keyboard.centerIconRow,
      "center",
      keyboardBox.x + keyboardBox.width / 2,
      y,
      rowAreaHeight,
    ),
    iconRowZoneNode(
      payload,
      keyboard.rightIconRow,
      "right",
      keyboardBox.x + keyboardBox.width - edgePadding,
      y,
      rowAreaHeight,
    ),
  ].filter((node): node is RenderableNode => node !== undefined);
}

function iconRowZoneNode(
  payload: DesignPreviewPayload,
  iconRow: IconRowDesignContract,
  zone: "left" | "center" | "right",
  anchorX: number,
  y: number,
  rowAreaHeight: number,
): RenderableNode | undefined {
  if (iconRow.buttons.length === 0) return undefined;
  const size = measureIconRowComponent(payload, iconRow);
  const x = zone === "left"
    ? anchorX
    : zone === "right"
      ? anchorX - size.width
      : anchorX - size.width / 2;
  return iconRowComponentToRenderableAt(payload, iconRow, {
    x,
    y: y + (rowAreaHeight - size.height) / 2,
    width: size.width,
    height: size.height,
  });
}
