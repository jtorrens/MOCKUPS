import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  colorForMode,
  iconTokenStyle,
  numberToken,
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

export const KeyboardPopupWidthRatio = 1.3;
export const KeyboardPopupConnectorHeightRatio = 0.34;

export interface KeyboardPopupGeometry {
  shapeBox: RenderableBox;
  labelBox: RenderableBox;
  pathData: string;
  viewBox: string;
  connectorCenterX: number;
  connectorTopLeftX: number;
  connectorTopRightX: number;
  connectorBottomLeftX: number;
  connectorBottomRightX: number;
}

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
  return wrapMotionFrame(
    payload,
    node,
    keyboard.motion,
    keyboard.motionFrame,
    outerBox,
    outerBox,
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
    const showPopup = pressed && keyboard.pressedEffect === "popup" && displayText.length > 0;
    if (showPopup) {
      return keyboardPopoverNodes(
        keyboard,
        `${rowIndex}.${index}`,
        keyboardBox,
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
      );
    }

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
  keyboardBox: RenderableBox,
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
  const geometry = keyboardPopupGeometry(keyboardBox, keyBox, radius);

  return [
    {
      id: `${keyboard.id}.key.${idSuffix}.popover`,
      type: "path",
      frame: 0,
      box: geometry.shapeBox,
      style: {
        fill: background,
        filter: dropShadowFilter(keyShadow),
        overflow: "visible",
        pathData: geometry.pathData,
        preserveAspectRatio: "none",
        stroke: borderWidth > 0 ? borderColor : undefined,
        strokeWidth: borderWidth > 0 ? borderWidth : undefined,
        vectorEffect: "non-scaling-stroke",
        viewBox: geometry.viewBox,
        zIndex: 20,
      },
      children: [
        {
          id: `${keyboard.id}.key.${idSuffix}.popover.text`,
          type: "text",
          frame: 0,
          box: geometry.labelBox,
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
            lineHeight: geometry.labelBox.height,
            textAlign: "center",
            whiteSpace: "nowrap",
            zIndex: 21,
          },
        },
      ],
    },
  ];
}

export function keyboardPopupGeometry(
  keyboardBox: RenderableBox,
  keyBox: RenderableBox,
  radius: number,
): KeyboardPopupGeometry {
  const headWidth = Math.min(
    keyBox.width * KeyboardPopupWidthRatio,
    keyboardBox.width,
  );
  const preferredHeadX = keyBox.x + keyBox.width / 2 - headWidth / 2;
  const headX = Math.max(
    keyboardBox.x,
    Math.min(
      preferredHeadX,
      keyboardBox.x + keyboardBox.width - headWidth,
    ),
  );
  const headHeight = keyBox.height;
  const connectorHeight = keyBox.height * KeyboardPopupConnectorHeightRatio;
  const totalHeight = headHeight + connectorHeight + keyBox.height;
  const baseTop = headHeight + connectorHeight;
  const baseLeft = keyBox.x - headX;
  const baseRight = baseLeft + keyBox.width;
  const connectorCenterX = keyBox.x + keyBox.width / 2 - headX;
  const connectorTopLeft = 0;
  const connectorTopRight = headWidth;
  const connectorBottomLeft = baseLeft;
  const connectorBottomRight = baseRight;
  const headRadius = Math.max(0, Math.min(radius, headWidth / 2, headHeight / 2));
  const keyRadius = Math.max(0, Math.min(radius, keyBox.width / 2, keyBox.height / 2));
  const shapeBox = {
    x: headX,
    y: keyBox.y - headHeight - connectorHeight,
    width: headWidth,
    height: totalHeight,
  };
  const pathData = [
    `M${headRadius} 0`,
    `H${headWidth - headRadius}`,
    `Q${headWidth} 0 ${headWidth} ${headRadius}`,
    `V${headHeight}`,
    `L${connectorBottomRight} ${baseTop}`,
    `V${totalHeight - keyRadius}`,
    `Q${baseRight} ${totalHeight} ${baseRight - keyRadius} ${totalHeight}`,
    `H${baseLeft + keyRadius}`,
    `Q${baseLeft} ${totalHeight} ${baseLeft} ${totalHeight - keyRadius}`,
    `V${baseTop}`,
    `L${connectorTopLeft} ${headHeight}`,
    `V${headRadius}`,
    `Q0 0 ${headRadius} 0`,
    "Z",
  ].join(" ");
  return {
    shapeBox,
    labelBox: {
      x: headX,
      y: shapeBox.y,
      width: headWidth,
      height: headHeight,
    },
    pathData,
    viewBox: `0 0 ${headWidth} ${totalHeight}`,
    connectorCenterX,
    connectorTopLeftX: connectorTopLeft,
    connectorTopRightX: connectorTopRight,
    connectorBottomLeftX: connectorBottomLeft,
    connectorBottomRightX: connectorBottomRight,
  };
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
