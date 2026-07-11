import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  colorForMode,
  iconTokenStyle,
  numberToken,
  previewScreenBox,
  renderScale,
  selectedColor,
  shadow,
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
  const keyboardBox = {
    x: payload.previewFrame.screenX,
    y: payload.previewFrame.screenY + payload.previewFrame.screenHeight - height,
    width,
    height,
  };
  const outerBox = keyboardBox;

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
    const fontScale = isEmojiKey ? keyboard.emojiScale : 1;
    const pressedScale = keyboard.pressedEffect === "scale" && pressed ? 0.94 : 1;
    const box = pressedScale < 1 ? scaleBox(baseBox, pressedScale) : baseBox;
    const background = pressed && keyboard.pressedEffect !== "popup"
      ? pressedKeyBackground
      : isSpecial
        ? specialKeyBackground
        : keyBackground;
    const fontSize = Math.max(8, typography.fontSize * fontScale);
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
          fontWeight: typography.fontWeight,
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
        keyBorderColor,
        keyBorderWidth,
      ));
    }

    return keyNodes;
  });
}

function isPressedKey(key: KeyboardKeyContract, pressedKey: string) {
  return pressedKey.length > 0 && (pressedKey === key.id || pressedKey === key.label);
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
  borderColor: string,
  borderWidth: number,
): RenderableNode[] {
  const popoverWidth = keyBox.width;
  const popoverHeight = keyBox.height;
  const tailHeight = keyBox.height * 0.44;
  const gap = tailHeight;
  const bodyBox = {
    x: keyBox.x + keyBox.width / 2 - popoverWidth / 2,
    y: keyBox.y - popoverHeight - gap,
    width: popoverWidth,
    height: popoverHeight,
  };
  const tailWidth = popoverWidth;
  const tailTipWidth = Math.min(keyBox.width * 0.34, popoverWidth * 0.34);
  const shapeBox = {
    x: bodyBox.x,
    y: bodyBox.y,
    width: popoverWidth,
    height: popoverHeight + tailHeight,
  };
  const pathData = popoverPathData(
    popoverWidth,
    popoverHeight,
    tailWidth,
    tailTipWidth,
    tailHeight,
    radius,
  );

  return [
    {
      id: `${keyboard.id}.key.${idSuffix}.popover`,
      type: "path",
      frame: 0,
      box: shapeBox,
      style: {
        fill: background,
        filter: dropShadowFilter(keyShadow),
        overflow: "visible",
        pathData,
        preserveAspectRatio: "none",
        stroke: borderWidth > 0 ? borderColor : undefined,
        strokeWidth: borderWidth > 0 ? borderWidth : undefined,
        vectorEffect: "non-scaling-stroke",
        viewBox: `0 0 ${popoverWidth} ${popoverHeight + tailHeight}`,
        zIndex: 20,
      },
      children: [
        {
          id: `${keyboard.id}.key.${idSuffix}.popover.text`,
          type: "text",
          frame: 0,
          box: bodyBox,
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
            zIndex: 21,
          },
        },
      ],
    },
  ];
}

function popoverPathData(
  width: number,
  bodyHeight: number,
  tailWidth: number,
  tailTipWidth: number,
  tailHeight: number,
  radius: number,
) {
  const r = Math.max(0, Math.min(radius, width / 2, bodyHeight / 2));
  const center = width / 2;
  const tailLeft = center - tailWidth / 2;
  const tailRight = center + tailWidth / 2;
  const tailTipLeft = center - tailTipWidth / 2;
  const tailTipRight = center + tailTipWidth / 2;
  const tailBottom = bodyHeight + tailHeight;
  return [
    `M${r} 0`,
    `H${width - r}`,
    `Q${width} 0 ${width} ${r}`,
    `V${bodyHeight - r}`,
    `Q${width} ${bodyHeight} ${width - r} ${bodyHeight}`,
    `H${tailRight}`,
    `L${tailTipRight} ${tailBottom}`,
    `H${tailTipLeft}`,
    `L${tailLeft} ${bodyHeight}`,
    `H${r}`,
    `Q0 ${bodyHeight} 0 ${bodyHeight - r}`,
    `V${r}`,
    `Q0 0 ${r} 0`,
    "Z",
  ].join(" ");
}

function dropShadowFilter(value: Record<string, unknown> | undefined) {
  if (!value) return undefined;
  const offsetX = typeof value.offsetX === "number" ? value.offsetX : 0;
  const offsetY = typeof value.offsetY === "number" ? value.offsetY : 0;
  const blur = typeof value.blur === "number" ? value.blur : 0;
  const color = typeof value.color === "string" ? value.color : "";
  return color ? `drop-shadow(${offsetX}px ${offsetY}px ${blur}px ${color})` : undefined;
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
