import type { RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  iconTokenStyle,
  renderScale,
  selectedColor,
  shadow,
  surfaceVisualPadding,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";
import type { VideoDesignContract } from "./videoComponentContract.js";

export function videoComponentToRenderable(
  payload: DesignPreviewPayload,
  video: VideoDesignContract,
): RenderableNode {
  const scale = renderScale(payload);
  const width = Math.min(payload.previewFrame.screenWidth * 0.78, 520 * scale);
  const height = width * 9 / 16;
  const borderWidth = video.surface.surface.borderWidth * scale;
  const surfaceRelief = video.surface.surface.reliefEnabled
    ? {
        angleDeg: video.surface.surface.reliefAngle,
        extension: video.surface.surface.reliefExtent * scale,
        spread: video.surface.surface.reliefSpread * scale,
        upperIntensity:
          video.surface.surface.reliefTopIntensity * video.surface.backgroundAlpha,
        lowerIntensity:
          video.surface.surface.reliefBottomIntensity * video.surface.backgroundAlpha,
      }
    : undefined;
  const videoShadow = video.surface.surface.shadowEnabled ? shadow(payload) : undefined;
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
  const videoSurfaceNode = surfaceComponentToRenderableAt(
    payload,
    video.surface,
    videoBox,
  );

  return {
    id: video.id,
    type: "group",
    frame: 0,
    box: outerBox,
    style: {
      overflow: "visible",
    },
    children: [
      {
        ...videoSurfaceNode,
        id: `${video.id}.surface`,
        style: {
          ...videoSurfaceNode.style,
          overflow: "hidden",
        },
      },
      ...statusNodes,
      ...playNodes,
    ],
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
        type: "icon",
        frame: 0,
        box,
        text: token,
        style: {
          ...iconTokenStyle(payload, token, statusTextColor),
        },
      } satisfies RenderableNode;
    });
  const durationWidth = Math.max(1, video.durationText.length * statusHeight * 0.42);
  return [
    {
      id: `${video.id}.status.background`,
      type: "surface",
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
