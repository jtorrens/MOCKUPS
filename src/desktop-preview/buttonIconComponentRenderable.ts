import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { ButtonIconDesignContract } from "./buttonIconComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  boundedCenterBox,
  colorForMode,
  expandBox,
  iconTokenStyle,
  numberToken,
  placeChild,
  renderScale,
  scalePlacement,
  selectedColor,
  selectedPaletteColor,
  shadow,
  surfaceVisualPadding,
  translateBox,
  unionBoxes,
  variants,
} from "./componentRenderableCommon.js";
import {
  labelComponentToRenderableAt,
  measureLabelComponent,
} from "./labelComponentRenderable.js";

export function buttonIconComponentToRenderable(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const iconSize = buttonIcon.iconSize * scale;
  const iconPadding = buttonIcon.iconPadding * scale;
  const surfaceSize = iconSize + iconPadding * 2;
  const iconShadow = buttonIcon.surface.shadowEnabled ? shadow(payload) : undefined;
  const labelSize = buttonIcon.labelSlot.label
    ? measureLabelComponent(buttonIcon.labelSlot.label, payload)
    : undefined;
  const buttonLocalBox = { x: 0, y: 0, width: surfaceSize, height: surfaceSize };
  const labelLocalBox = labelSize
    ? placeChild(
        buttonLocalBox,
        labelSize,
        scalePlacement(buttonIcon.labelSlot.placement, scale),
      )
    : undefined;
  const contentBounds = unionBoxes([
    buttonLocalBox,
    ...(labelLocalBox ? [labelLocalBox] : []),
  ]);
  const borderWidth = buttonIcon.surface.borderWidth * scale;
  const surfaceRelief = buttonIconSurfaceRelief(buttonIcon, scale);
  const visualPadding = surfaceVisualPadding(borderWidth, iconShadow, surfaceRelief);
  const groupBox = boundedCenterBox(
    payload,
    contentBounds.width + visualPadding * 2,
    contentBounds.height + visualPadding * 2,
  );
  const contentOrigin = {
    x: groupBox.x + visualPadding - contentBounds.x,
    y: groupBox.y + visualPadding - contentBounds.y,
  };
  const buttonBox = translateBox(buttonLocalBox, contentOrigin);
  const labelBox = labelLocalBox ? translateBox(labelLocalBox, contentOrigin) : undefined;
  const iconBox = {
    x: buttonBox.x + iconPadding,
    y: buttonBox.y + iconPadding,
    width: iconSize,
    height: iconSize,
  };

  return buttonIconRenderableNode(
    payload,
    buttonIcon,
    groupBox,
    buttonBox,
    iconBox,
    labelBox,
  );
}

export function buttonIconComponentToRenderableAt(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
  buttonBox: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const iconPadding = buttonIcon.iconPadding * scale;
  const iconSize = Math.max(1, buttonBox.width - iconPadding * 2);
  const iconShadow = buttonIcon.surface.shadowEnabled ? shadow(payload) : undefined;
  const labelSize = buttonIcon.labelSlot.label
    ? measureLabelComponent(buttonIcon.labelSlot.label, payload)
    : undefined;
  const labelBox = labelSize
    ? placeChild(
        buttonBox,
        labelSize,
        scalePlacement(buttonIcon.labelSlot.placement, scale),
      )
    : undefined;
  const borderWidth = buttonIcon.surface.borderWidth * scale;
  const surfaceRelief = buttonIconSurfaceRelief(buttonIcon, scale);
  const visualPadding = surfaceVisualPadding(borderWidth, iconShadow, surfaceRelief);
  const contentBounds = unionBoxes([
    buttonBox,
    ...(labelBox ? [labelBox] : []),
  ]);
  const groupBox = expandBox(contentBounds, visualPadding);
  const iconBox = {
    x: buttonBox.x + iconPadding,
    y: buttonBox.y + iconPadding,
    width: iconSize,
    height: iconSize,
  };

  return buttonIconRenderableNode(
    payload,
    buttonIcon,
    groupBox,
    buttonBox,
    iconBox,
    labelBox,
  );
}

function buttonIconRenderableNode(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
  groupBox: RenderableBox,
  buttonBox: RenderableBox,
  iconBox: RenderableBox,
  labelBox: RenderableBox | undefined,
): RenderableNode {
  const scale = renderScale(payload);
  const iconShadow = buttonIcon.surface.shadowEnabled ? shadow(payload) : undefined;
  const borderWidth = buttonIcon.surface.borderWidth * scale;
  const surfaceRelief = buttonIconSurfaceRelief(buttonIcon, scale);

  return {
    id: buttonIcon.id,
    type: "group",
    role: "buttonIcon",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${buttonIcon.id}.surface`,
        type: "surface",
        role: "buttonIconSurface",
        frame: 0,
        box: buttonBox,
        style: {
          background: buttonBackgroundColor(payload, buttonIcon),
          borderRadius: numberToken(payload, buttonIcon.surface.cornerRadiusToken) * scale,
          borderWidth,
          borderColor: selectedColor(payload, buttonIcon.surface.borderColorToken),
          shadow: iconShadow,
          surfaceRelief,
          colorModes: Object.fromEntries(
            variants(payload).map((mode) => [
              mode,
              {
                background: buttonBackgroundColorForMode(payload, buttonIcon, mode),
                color: buttonIconColorForMode(payload, buttonIcon, mode),
                borderColor: colorForMode(
                  payload,
                  buttonIcon.surface.borderColorToken,
                  mode,
                ),
              },
            ]),
          ),
        },
      },
      {
        id: `${buttonIcon.id}.glyph`,
        type: "icon_token",
        role: "iconToken",
        frame: 0,
        box: iconBox,
        text: buttonIcon.iconToken,
        style: {
          ...iconTokenStyle(
            payload,
            buttonIcon.iconToken,
            buttonIconColor(payload, buttonIcon),
          ),
        },
        metadata: {
          token: buttonIcon.iconToken,
        },
      },
      ...(buttonIcon.labelSlot.label && labelBox
        ? [
            labelComponentToRenderableAt(
              payload,
              buttonIcon.labelSlot.label,
              labelBox,
            ),
          ]
        : []),
    ],
  };
}

function buttonIconSurfaceRelief(
  buttonIcon: ButtonIconDesignContract,
  scale: number,
) {
  return buttonIcon.surface.reliefEnabled
    ? {
        angleDeg: buttonIcon.surface.reliefAngle,
        extension: buttonIcon.surface.reliefExtent * scale,
        spread: buttonIcon.surface.reliefSpread * scale,
        upperIntensity: buttonIcon.surface.reliefTopIntensity * buttonIcon.backgroundAlpha,
        lowerIntensity: buttonIcon.surface.reliefBottomIntensity * buttonIcon.backgroundAlpha,
      }
    : undefined;
}

function buttonBackgroundColor(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
  alpha = buttonIcon.backgroundAlpha,
) {
  return buttonIcon.backgroundPaletteColor
    ? selectedPaletteColor(payload, buttonIcon.backgroundPaletteColor, alpha)
    : selectedColor(payload, buttonIcon.backgroundColorToken, alpha);
}

function buttonIconColor(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
) {
  return buttonIcon.iconPaletteColor
    ? selectedPaletteColor(payload, buttonIcon.iconPaletteColor)
    : selectedColor(payload, buttonIcon.iconColorToken);
}

function buttonBackgroundColorForMode(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
  mode: string,
) {
  return buttonIcon.backgroundPaletteColor
    ? selectedPaletteColor(
        payload,
        buttonIcon.backgroundPaletteColor,
        buttonIcon.backgroundAlpha,
      )
    : colorForMode(
        payload,
        buttonIcon.backgroundColorToken,
        mode,
        buttonIcon.backgroundAlpha,
      );
}

function buttonIconColorForMode(
  payload: DesignPreviewPayload,
  buttonIcon: ButtonIconDesignContract,
  mode: string,
) {
  return buttonIcon.iconPaletteColor
    ? selectedPaletteColor(payload, buttonIcon.iconPaletteColor)
    : colorForMode(payload, buttonIcon.iconColorToken, mode);
}
