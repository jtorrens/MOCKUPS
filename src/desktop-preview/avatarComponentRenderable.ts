import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  boundedCenterBox,
  expandBox,
  numberToken,
  placeChild,
  renderScale,
  scalePlacement,
  selectedColor,
  shadow,
  surfaceVisualPadding,
  translateBox,
  unionBoxes,
} from "./componentRenderableCommon.js";
import {
  labelComponentToRenderableAt,
  measureLabelComponent,
} from "./labelComponentRenderable.js";

export function avatarComponentToRenderable(
  payload: DesignPreviewPayload,
  avatar: AvatarDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const avatarSize = avatar.size * scale;
  const avatarShadow = avatar.surface.shadowEnabled ? shadow(payload) : undefined;
  const labelSize = avatar.labelSlot.label
    ? measureLabelComponent(avatar.labelSlot.label, payload)
    : undefined;
  const avatarLocalBox = { x: 0, y: 0, width: avatarSize, height: avatarSize };
  const labelLocalBox = labelSize
    ? placeChild(
        avatarLocalBox,
        labelSize,
        scalePlacement(avatar.labelSlot.placement, scale),
      )
    : undefined;
  const contentBounds = unionBoxes([
    avatarLocalBox,
    ...(labelLocalBox ? [labelLocalBox] : []),
  ]);
  const borderWidth = avatar.surface.borderWidth * scale;
  const surfaceRelief = avatar.surface.reliefEnabled
    ? {
        angleDeg: avatar.surface.reliefAngle,
        extension: avatar.surface.reliefExtent * scale,
        spread: avatar.surface.reliefSpread * scale,
        upperIntensity: avatar.surface.reliefTopIntensity,
        lowerIntensity: avatar.surface.reliefBottomIntensity,
      }
    : undefined;
  const visualPadding = surfaceVisualPadding(borderWidth, avatarShadow, surfaceRelief);
  const groupBox = boundedCenterBox(
    payload,
    contentBounds.width + visualPadding * 2,
    contentBounds.height + visualPadding * 2,
  );
  const contentOrigin = {
    x: groupBox.x + visualPadding - contentBounds.x,
    y: groupBox.y + visualPadding - contentBounds.y,
  };
  const avatarBox = translateBox(avatarLocalBox, contentOrigin);
  const labelBox = labelLocalBox ? translateBox(labelLocalBox, contentOrigin) : undefined;

  return avatarRenderableNode(payload, avatar, groupBox, avatarBox, labelBox);
}

export function avatarComponentToRenderableAt(
  payload: DesignPreviewPayload,
  avatar: AvatarDesignContract,
  avatarBox: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const avatarShadow = avatar.surface.shadowEnabled ? shadow(payload) : undefined;
  const labelSize = avatar.labelSlot.label
    ? measureLabelComponent(avatar.labelSlot.label, payload)
    : undefined;
  const labelBox = labelSize
    ? placeChild(
        avatarBox,
        labelSize,
        scalePlacement(avatar.labelSlot.placement, scale),
      )
    : undefined;
  const borderWidth = avatar.surface.borderWidth * scale;
  const surfaceRelief = avatar.surface.reliefEnabled
    ? {
        angleDeg: avatar.surface.reliefAngle,
        extension: avatar.surface.reliefExtent * scale,
        spread: avatar.surface.reliefSpread * scale,
        upperIntensity: avatar.surface.reliefTopIntensity,
        lowerIntensity: avatar.surface.reliefBottomIntensity,
      }
    : undefined;
  const visualPadding = surfaceVisualPadding(borderWidth, avatarShadow, surfaceRelief);
  const contentBounds = unionBoxes([
    avatarBox,
    ...(labelBox ? [labelBox] : []),
  ]);
  const groupBox = expandBox(contentBounds, visualPadding);

  return avatarRenderableNode(payload, avatar, groupBox, avatarBox, labelBox);
}

function avatarRenderableNode(
  payload: DesignPreviewPayload,
  avatar: AvatarDesignContract,
  groupBox: RenderableBox,
  avatarBox: RenderableBox,
  labelBox: RenderableBox | undefined,
): RenderableNode {
  const scale = renderScale(payload);
  const avatarShadow = avatar.surface.shadowEnabled ? shadow(payload) : undefined;
  const borderWidth = avatar.surface.borderWidth * scale;
  const surfaceRelief = avatar.surface.reliefEnabled
    ? {
        angleDeg: avatar.surface.reliefAngle,
        extension: avatar.surface.reliefExtent * scale,
        spread: avatar.surface.reliefSpread * scale,
        upperIntensity: avatar.surface.reliefTopIntensity,
        lowerIntensity: avatar.surface.reliefBottomIntensity,
      }
    : undefined;

  return {
    id: avatar.id,
    type: "group",
    role: "avatar",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${avatar.id}.placeholder`,
        type: "image",
        frame: 0,
        box: avatarBox,
        text: avatar.actor.initials.toUpperCase(),
        style: {
          alignItems: "center",
          background: avatar.actor.avatar.backgroundColor,
          borderRadius: numberToken(payload, avatar.cornerRadiusToken) * scale,
          borderWidth,
          borderColor: selectedColor(payload, avatar.surface.borderColorToken),
          color: avatar.actor.avatar.textColor,
          display: "flex",
          fontSize: avatarBox.width * 0.45,
          fontWeight: 700,
          justifyContent: "center",
          overflow: "hidden",
          position: "relative",
          shadow: avatarShadow,
          surfaceRelief,
        },
        asset: avatar.actor.avatar.imageUri
          ? {
              type: "image",
              uri: avatar.actor.avatar.imageUri,
            }
          : undefined,
        metadata: {
          fallbackText: avatar.actor.initials.toUpperCase(),
          imageBaseSize: avatar.actor.avatar.baseSize,
          imageOffsetX: avatar.actor.avatar.offsetX,
          imageOffsetY: avatar.actor.avatar.offsetY,
          imageScale: avatar.actor.avatar.scale,
        },
      },
      ...(avatar.labelSlot.label && labelBox
        ? [
            labelComponentToRenderableAt(
              payload,
              avatar.labelSlot.label,
              labelBox,
            ),
          ]
        : []),
    ],
  };
}
