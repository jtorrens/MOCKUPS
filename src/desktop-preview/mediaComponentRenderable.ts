import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  numberToken,
  placeChild,
  previewScreenBox,
  renderScale,
  scalePlacement,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { iconBarComponentToRenderableAt } from "./iconBarComponentRenderable.js";
import type { IconBarDesignContract } from "./iconBarComponentContract.js";
import type { MediaDesignContract, MediaRenderBoxes } from "./mediaComponentContract.js";
import { mediaFrameUriForPath } from "./previewAssetResolver.js";
import { motionFrameProgress } from "./previewMotionHelpers.js";
import { labelComponentToRenderableAt, measureLabelComponent } from "./labelComponentRenderable.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function mediaComponentToRenderable(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
): RenderableNode {
  const boxes = mediaBoxes(payload, media);
  return mediaComponentToRenderableForBoxes(payload, media, boxes);
}

export function measureMediaComponent(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
): { width: number; height: number } {
  const scale = renderScale(payload);
  return {
    width: Math.max(1, media.viewport.width * scale),
    height: Math.max(1, media.viewport.height * scale),
  };
}

export function mediaComponentToRenderableAt(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  box: RenderableBox,
): RenderableNode {
  return mediaComponentToRenderableForBoxes(
    payload,
    media,
    mediaBoxesFromInlineBox(payload, media, sizedInlineMediaBox(payload, media, box)),
  );
}

function mediaComponentToRenderableForBoxes(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  boxes: MediaRenderBoxes,
): RenderableNode {
  const mediaSurfaceNode = surfaceComponentToRenderableAt(payload, media.surface, boxes.media);
  const mediaContentNode = mediaContent(payload, media, boxes.media);
  const controlNodes = mediaControlNodes(payload, media, boxes.media);
  const children = [
    ...mediaBars(payload, media, boxes),
    mediaSurfaceNode,
    mediaVisualClipNode(payload, media, boxes.media, [mediaContentNode, ...controlNodes]),
  ];
  const node = {
    id: media.id,
    type: "group",
    frame: 0,
    box: boxes.root,
    style: {
      overflow: "visible",
      ...(media.displayState === "fullframe" ? { rootOverlay: true, zIndex: 1000 } : {}),
    },
    children,
  } satisfies RenderableNode;
  return node;
}

function mediaVisualClipNode(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  box: RenderableBox,
  children: RenderableNode[],
): RenderableNode {
  return {
    id: `${media.id}.visualClip`,
    type: "group",
    frame: 0,
    box,
    style: {
      borderRadius: numberToken(payload, media.surface.surface.cornerRadiusToken) * renderScale(payload),
      overflow: "hidden",
    },
    children,
  };
}

function mediaBoxes(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
): MediaRenderBoxes {
  return mediaBoxesFromInlineBox(payload, media, inlineMediaBox(payload, media));
}

function mediaBoxesFromInlineBox(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  inlineBox: RenderableBox,
): MediaRenderBoxes {
  const inline = {
    root: inlineBox,
    media: inlineBox,
  };
  if (media.displayState !== "fullframe") {
    return inline;
  }

  const fullframe = fullframeMediaBoxes(payload, media);
  const progress = mediaFullframeTransitionProgress(payload, media);
  if (!media.motionFrame.trigger || progress >= 1) {
    return fullframe;
  }

  return {
    root: interpolateBox(inline.root, fullframe.root, progress),
    media: interpolateBox(inline.media, fullframe.media, progress),
  };
}

function inlineMediaBox(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
): RenderableBox {
  const scale = renderScale(payload);
  const width = media.viewport.width * scale;
  const height = media.viewport.height * scale;
  return boundedCenterBox(payload, width, height);
}

function sizedInlineMediaBox(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  box: RenderableBox,
): RenderableBox {
  const size = measureMediaComponent(payload, media);
  return {
    x: box.x,
    y: box.y,
    width: box.width || size.width,
    height: box.height || size.height,
  };
}

function fullframeMediaBoxes(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
): MediaRenderBoxes {
  const root = previewScreenBox(payload);
  return {
    root,
    media: fitAspect(
      root,
      media.fullframeOrientation === "landscape"
        ? Math.max(root.width, root.height) / Math.min(root.width, root.height)
        : root.width / root.height,
    ),
  };
}

function interpolateBox(
  start: RenderableBox,
  end: RenderableBox,
  progress: number,
): RenderableBox {
  const clamped = Math.max(0, Math.min(1, progress));
  return {
    x: lerp(start.x, end.x, clamped),
    y: lerp(start.y, end.y, clamped),
    width: lerp(start.width, end.width, clamped),
    height: lerp(start.height, end.height, clamped),
  };
}

function lerp(start: number, end: number, amount: number) {
  return start + (end - start) * amount;
}

function fitAspect(box: RenderableBox, aspect: number): RenderableBox {
  const safeAspect = Number.isFinite(aspect) && aspect > 0 ? aspect : 1;
  const boxAspect = box.width / box.height;
  if (boxAspect > safeAspect) {
    const width = box.height * safeAspect;
    return {
      x: box.x + (box.width - width) / 2,
      y: box.y,
      width,
      height: box.height,
    };
  }

  const height = box.width / safeAspect;
  return {
    x: box.x,
    y: box.y + (box.height - height) / 2,
    width: box.width,
    height,
  };
}

function mediaBars(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  boxes: MediaRenderBoxes,
): RenderableNode[] {
  if (media.displayState !== "fullframe") return [];
  const transitionProgress = mediaFullframeTransitionProgress(payload, media);
  const isTransitioning = media.motionFrame.trigger && transitionProgress < 1;
  const transitionRadius = numberToken(payload, media.surface.surface.cornerRadiusToken) * renderScale(payload);
  const bars: RenderableNode[] = [
    {
      id: `${media.id}.fullframeBackground`,
      type: "surface",
      frame: 0,
      box: boxes.root,
      style: {
        background: "#000000",
        borderRadius: isTransitioning ? transitionRadius : 0,
        overflow: "hidden",
      },
    },
  ];

  return bars;
}

function mediaFullframeTransitionProgress(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
) {
  return motionFrameProgress(payload, media.motion, media.motionFrame);
}

function mediaContent(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  box: RenderableBox,
): RenderableNode {
  const frameTimeSeconds = media.mediaKind === "video" ? media.currentTimeSeconds : 0;
  const frame = mediaFrameUriForPath(payload, media.sourceUri, frameTimeSeconds);
  const uri = frame.uri;
  if (uri) {
    return {
      id: `${media.id}.content`,
      type: "image",
      frame: 0,
      box,
      asset: {
        type: "image",
        uri,
      },
      style: {
        objectFit: "cover",
      },
      metadata: {
        imageBaseSize: media.viewport.width,
        imageOffsetX: media.viewport.offsetX,
        imageOffsetY: media.viewport.offsetY,
        imageScale: media.viewport.scale,
      },
    };
  }

  return mediaPlaceholder(
    media,
    box,
    frame.error ?? "Media frame pending",
  );
}

function mediaPlaceholder(
  media: MediaDesignContract,
  box: RenderableBox,
  label: string,
): RenderableNode {
  return {
    id: `${media.id}.placeholder`,
    type: "surface",
    frame: 0,
    box,
    text: label,
    style: {
      alignItems: "center",
      background: "rgba(0, 0, 0, 0.42)",
      color: "#ffffff",
      display: "flex",
      fontSize: Math.max(11, box.height * 0.055),
      fontWeight: 700,
      justifyContent: "center",
      lineHeight: box.height,
      textAlign: "center",
    },
  };
}

function mediaControlNodes(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  mediaBox: RenderableBox,
): RenderableNode[] {
  const opacity = controlsOpacity(media);
  if (opacity <= 0) return [];
  const scale = renderScale(payload);
  const paddingX = Math.max(0, numberToken(payload, media.iconBarPadding.xToken) * scale);
  const paddingY = Math.max(0, numberToken(payload, media.iconBarPadding.yToken) * scale);
  const topHeight = media.topIconBar.size.height * renderScale(payload);
  const bottomHeight = media.bottomIconBar.size.height * renderScale(payload);
  const controlsBox = mediaBox;
  const paddedBox = insetBox(mediaBox, paddingX, paddingY);
  const topBox = {
    x: paddedBox.x,
    y: paddedBox.y,
    width: paddedBox.width,
    height: topHeight,
  };
  const centerBox = {
    x: paddedBox.x,
    y: paddedBox.y,
    width: paddedBox.width,
    height: paddedBox.height,
  };
  const bottomBox = {
    x: paddedBox.x,
    y: paddedBox.y + paddedBox.height - bottomHeight,
    width: paddedBox.width,
    height: bottomHeight,
  };
  return [
    {
      id: `${media.id}.controls`,
      type: "group",
      frame: 0,
      box: controlsBox,
      transform: {
        opacity,
      },
      style: {
        overflow: "visible",
      },
      children: [
        iconBarNode(payload, media.topIconBar, topBox),
        iconBarNode(payload, media.centerIconBar, centerBox),
        iconBarNode(payload, media.bottomIconBar, bottomBox),
        ...mediaTextOverlayNodes(payload, media, mediaBox),
      ],
    },
  ];
}

function iconBarNode(
  payload: DesignPreviewPayload,
  iconBar: IconBarDesignContract,
  box: RenderableBox,
) {
  return iconBarComponentToRenderableAt(payload, iconBar, box);
}

function insetBox(
  box: RenderableBox,
  paddingX: number,
  paddingY: number,
): RenderableBox {
  return {
    x: box.x + paddingX,
    y: box.y + paddingY,
    width: Math.max(1, box.width - paddingX * 2),
    height: Math.max(1, box.height - paddingY * 2),
  };
}

function controlsOpacity(media: MediaDesignContract) {
  if (media.controlsFadeDurationMs <= 0) {
    return media.controlsElapsedMs > media.controlsFadeDelayMs ? 0 : 1;
  }
  if (media.controlsElapsedMs <= media.controlsFadeDelayMs) return 1;
  const fadeProgress =
    (media.controlsElapsedMs - media.controlsFadeDelayMs) /
    media.controlsFadeDurationMs;
  return Math.max(0, Math.min(1, 1 - fadeProgress));
}

function mediaTextOverlayNodes(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  mediaBox: RenderableBox,
): RenderableNode[] {
  const overlay = media.textOverlay;
  if (!overlay?.enabled || overlay.label.text.trim().length === 0) {
    return [];
  }

  const scale = renderScale(payload);
  const textSize = measureLabelComponent(overlay.label, payload);
  const childSize = {
    width: Math.min(mediaBox.width, Math.max(1, textSize.width)),
    height: Math.max(1, textSize.height),
  };
  const box = placeChild(
    mediaBox,
    childSize,
    scalePlacement(overlay.placement, scale),
  );

  return [
    labelComponentToRenderableAt(payload, overlay.label, box),
  ];
}
