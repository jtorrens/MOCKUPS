import type { RenderableNode } from "../visual/renderable/types.js";
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
import type { VideoDesignContract } from "./videoComponentContract.js";

export function videoComponentToRenderable(
  payload: DesignPreviewPayload,
  video: VideoDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const width = Math.min(payload.previewFrame.screenWidth * 0.78, 520 * scale);
  const height = width * 9 / 16;
  const borderWidth = video.surface.borderWidth * scale;
  const surfaceRelief = video.surface.reliefEnabled
    ? {
        angleDeg: video.surface.reliefAngle,
        extension: video.surface.reliefExtent * scale,
        spread: video.surface.reliefSpread * scale,
        upperIntensity: video.surface.reliefTopIntensity * video.backgroundAlpha,
        lowerIntensity: video.surface.reliefBottomIntensity * video.backgroundAlpha,
      }
    : undefined;
  const videoShadow = video.surface.shadowEnabled ? shadow(payload) : undefined;
  const visualPadding = surfaceVisualPadding(borderWidth, videoShadow, surfaceRelief);
  const outerBox = boundedCenterBox(
    payload,
    width + visualPadding * 2,
    height + visualPadding * 2,
  );
  const videoBox = {
    x: outerBox.x + visualPadding,
    y: outerBox.y + visualPadding,
    width,
    height,
  };
  const statusNodes = video.statusVisible
    ? videoStatusNodes(payload, video, videoBox)
    : [];
  const playNodes = video.playOverlayVisible
    ? videoPlayNodes(payload, video, videoBox)
    : [];

  return {
    id: video.id,
    type: "group",
    role: "video",
    frame: 0,
    box: outerBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        id: `${video.id}.surface`,
        type: "surface",
        role: "video_surface",
        frame: 0,
        box: videoBox,
        style: {
          background: selectedColor(
            payload,
            video.backgroundColorToken,
            video.backgroundAlpha,
          ),
          borderColor: selectedColor(payload, video.surface.borderColorToken),
          borderRadius: numberToken(payload, video.surface.cornerRadiusToken) * scale,
          borderWidth,
          shadow: videoShadow,
          surfaceRelief,
          overflow: "hidden",
          colorModes: Object.fromEntries(
            variants(payload).map((mode) => [
              mode,
              {
                background: colorForMode(
                  payload,
                  video.backgroundColorToken,
                  mode,
                  video.backgroundAlpha,
                ),
                borderColor: colorForMode(
                  payload,
                  video.surface.borderColorToken,
                  mode,
                ),
              },
            ]),
          ),
        },
      },
      ...statusNodes,
      ...playNodes,
    ],
    metadata: {
      route: "component-resolver.video-renderable",
      componentType: "video",
    },
  };
}

function videoStatusNodes(
  payload: DesignPreviewPayload,
  video: VideoDesignContract,
  videoBox: { x: number; y: number; width: number; height: number },
): RenderableNode[] {
  const scale = renderScale(payload);
  const statusHeight = Math.max(1, video.statusHeight * scale);
  const statusTextColor = selectedColor(payload, video.statusTextColorToken);
  const iconSize = Math.max(8 * scale, statusHeight * 0.58);
  const padding = Math.max(8 * scale, statusHeight * 0.4);
  const topBox = {
    x: videoBox.x,
    y: videoBox.y,
    width: videoBox.width,
    height: statusHeight,
  };
  const iconNodes = [...video.statusIconSlots.left, ...video.statusIconSlots.center, ...video.statusIconSlots.right]
    .slice(0, 5)
    .map((token, index) => {
      const box = {
        x: topBox.x + padding + index * (iconSize + padding * 0.5),
        y: topBox.y + (topBox.height - iconSize) / 2,
        width: iconSize,
        height: iconSize,
      };
      return {
        id: `${video.id}.status.icon.${index}`,
        type: "icon_token",
        role: "video_status_icon",
        frame: 0,
        box,
        text: token,
        style: {
          ...iconTokenStyle(payload, token, statusTextColor),
        },
        metadata: {
          token,
        },
      } satisfies RenderableNode;
    });
  const durationWidth = Math.max(1, video.durationText.length * statusHeight * 0.42);
  return [
    {
      id: `${video.id}.status.background`,
      type: "surface",
      role: "video_status_background",
      frame: 0,
      box: topBox,
      style: {
        background: "rgba(0, 0, 0, 0.18)",
      },
    },
    ...iconNodes,
    {
      id: `${video.id}.status.duration`,
      type: "text",
      role: "video_status_duration",
      frame: 0,
      box: {
        x: topBox.x + topBox.width - padding - durationWidth,
        y: topBox.y,
        width: durationWidth,
        height: topBox.height,
      },
      text: video.durationText,
      style: {
        color: statusTextColor,
        display: "flex",
        alignItems: "center",
        justifyContent: "flex-end",
        fontSize: Math.max(8, statusHeight * 0.48),
        lineHeight: topBox.height,
        textAlign: "right",
        whiteSpace: "nowrap",
      },
    },
  ];
}

function videoPlayNodes(
  payload: DesignPreviewPayload,
  video: VideoDesignContract,
  videoBox: { x: number; y: number; width: number; height: number },
): RenderableNode[] {
  const scale = renderScale(payload);
  const size = Math.max(34 * scale, Math.min(videoBox.width, videoBox.height) * 0.24);
  const box = {
    x: videoBox.x + (videoBox.width - size) / 2,
    y: videoBox.y + (videoBox.height - size) / 2,
    width: size,
    height: size,
  };
  return [
    {
      id: `${video.id}.play`,
      type: "surface",
      role: "video_play_overlay",
      frame: 0,
      box,
      text: "▶",
      style: {
        alignItems: "center",
        background: selectedColor(payload, video.playColorToken, 0.82),
        borderRadius: size / 2,
        color: selectedColor(payload, video.statusTextColorToken),
        display: "flex",
        fontSize: size * 0.52,
        justifyContent: "center",
        lineHeight: size,
        paddingLeft: size * 0.07,
        textAlign: "center",
      },
    },
  ];
}
