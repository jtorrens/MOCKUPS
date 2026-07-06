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
    type: "component_avatar",
    frame: 0,
    box: groupBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${avatar.id}.placeholder`,
        type: "avatar",
        frame: 0,
        box: avatarBox,
        style: {
          borderRadius: numberToken(payload, avatar.cornerRadiusToken) * scale,
          borderWidth,
          borderColor: selectedColor(payload, avatar.surface.borderColorToken),
          shadow: avatarShadow,
          surfaceRelief,
        },
        asset: {
          type: "image",
          uri: sampleAvatarUri(),
        },
        metadata: {
          label: "Avatar preview",
          imageBaseSize: 256,
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
    metadata: {
      route: "component-resolver.avatar-renderable",
      componentType: "avatar",
    },
  };
}

function sampleAvatarUri() {
  const svg = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256">
  <defs>
    <linearGradient id="bg" x1="40" y1="24" x2="216" y2="232" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#DCE6F3"/>
      <stop offset="0.52" stop-color="#AEBBD0"/>
      <stop offset="1" stop-color="#63738E"/>
    </linearGradient>
    <linearGradient id="skin" x1="86" y1="54" x2="170" y2="152" gradientUnits="userSpaceOnUse">
      <stop offset="0" stop-color="#F4C8AA"/>
      <stop offset="1" stop-color="#C98970"/>
    </linearGradient>
  </defs>
  <rect width="256" height="256" fill="url(#bg)"/>
  <circle cx="128" cy="99" r="50" fill="url(#skin)"/>
  <path d="M46 246c10-58 45-86 82-86s72 28 82 86H46z" fill="#26354F"/>
  <path d="M78 92c9-40 35-60 63-52 24 7 39 27 41 54-24-10-47-23-64-44-8 21-20 35-40 42z" fill="#3A2B26"/>
  <circle cx="109" cy="104" r="5" fill="#332A2A"/>
  <circle cx="148" cy="104" r="5" fill="#332A2A"/>
  <path d="M112 128c12 10 25 10 37 0" fill="none" stroke="#7E4D43" stroke-width="6" stroke-linecap="round"/>
</svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}
