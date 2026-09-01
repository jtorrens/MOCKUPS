import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  numberToken,
  placeChild,
  renderScale,
  rootPreviewScreenBox,
  scalePlacement,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { iconBarComponentToRenderableAt } from "./iconBarComponentRenderable.js";
import type { IconBarDesignContract } from "./iconBarComponentContract.js";
import type { MediaDesignContract, MediaRenderBoxes } from "./mediaComponentContract.js";
import { mediaFrameUriForPath } from "./previewAssetResolver.js";
import { renderAuthoringSlot } from "./previewAuthoringTarget.js";
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
  const visualBox = mediaVisualBox(payload, media, boxes.media);
  const mediaSurfaceNode = renderAuthoringSlot(
    payload,
    "component.media",
    "component.media.surface.editor",
    "component.surface",
    "component.surface.backgroundColorToken",
    (slotPayload) => surfaceComponentToRenderableAt(slotPayload, media.surface, boxes.media),
  );
  const mediaContentNode = mediaContent(payload, media, visualBox);
  const controlNodes = mediaControlNodes(payload, media, visualBox);
  const children = [
    ...mediaBars(payload, media, boxes),
    mediaSurfaceNode,
    mediaVisualClipNode(payload, media, visualBox, [mediaContentNode, ...controlNodes]),
  ];
  const transitionActive = media.motionFrame.active && media.motionFrame.progress < 1;
  const rootOverlay = media.displayState === "fullframe" || transitionActive;
  const rootOverlayTranslationFactor = transitionActive
    ? media.motionFrame.reverse
      ? media.motionFrame.progress
      : 1 - media.motionFrame.progress
    : 0;
  const node = {
    id: media.id,
    type: "group",
    frame: 0,
    box: boxes.root,
    style: {
      overflow: "visible",
      ...(rootOverlay
        ? {
            rootOverlay: true,
            rootOverlayTranslationFactor,
            zIndex: 1000,
          }
        : {}),
    },
    children,
  } satisfies RenderableNode;
  return node;
}

function mediaVisualBox(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  box: RenderableBox,
): RenderableBox {
  const borderWidth = Math.max(0, media.surface.surface.borderWidth * renderScale(payload));
  const insetX = Math.min(borderWidth, box.width / 2);
  const insetY = Math.min(borderWidth, box.height / 2);
  return {
    x: box.x + insetX,
    y: box.y + insetY,
    width: Math.max(0, box.width - insetX * 2),
    height: Math.max(0, box.height - insetY * 2),
  };
}

function mediaVisualClipNode(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  box: RenderableBox,
  children: RenderableNode[],
): RenderableNode {
  const borderWidth = Math.max(0, media.surface.surface.borderWidth * renderScale(payload));
  return {
    id: `${media.id}.visualClip`,
    type: "group",
    frame: 0,
    box,
    style: {
      borderRadius: Math.max(
        0,
        numberToken(payload, media.surface.surface.cornerRadiusToken) * renderScale(payload) - borderWidth,
      ),
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
  const fullframe = fullframeMediaBoxes(payload);
  const progress = media.motionFrame.progress;
  if (!media.motionFrame.active || progress >= 1) {
    return media.displayState === "fullframe" ? fullframe : inline;
  }

  const from = media.motionFrame.reverse ? fullframe : inline;
  const to = media.motionFrame.reverse ? inline : fullframe;

  return {
    root: interpolateBox(from.root, to.root, progress),
    media: interpolateBox(from.media, to.media, progress),
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
): MediaRenderBoxes {
  const root = rootPreviewScreenBox(payload);
  return {
    root,
    media: root,
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

function mediaBars(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  boxes: MediaRenderBoxes,
): RenderableNode[] {
  const transitionProgress = media.motionFrame.progress;
  const isTransitioning = media.motionFrame.active && transitionProgress < 1;
  if (media.displayState !== "fullframe" && !isTransitioning) return [];
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

function mediaContent(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  box: RenderableBox,
): RenderableNode {
  const frameTimeSeconds = media.mediaKind === "video" ? media.currentTimeSeconds : 0;
  const frame = mediaFrameUriForPath(payload, media.sourceUri, frameTimeSeconds);
  const uri = frame.uri;
  if (uri) {
    const customPlacement = Math.abs(media.viewport.scale - 1) > 0.000001
      || Math.abs(media.viewport.offsetX) > 0.000001
      || Math.abs(media.viewport.offsetY) > 0.000001;
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
        ...(frame.width && frame.height
          ? {
              imageIntrinsicHeight: frame.height,
              imageIntrinsicWidth: frame.width,
            }
          : {}),
        ...(customPlacement
          ? {
              imageOffsetX: media.viewport.offsetX,
              imageOffsetY: media.viewport.offsetY,
              imageScale: media.viewport.scale,
            }
          : {}),
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
  const opacity = media.controlsOpacity;
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
        iconBarNode(payload, media, "Top", media.topIconBar, topBox),
        iconBarNode(payload, media, "Center", media.centerIconBar, centerBox),
        iconBarNode(payload, media, "Bottom", media.bottomIconBar, bottomBox),
        ...mediaTextOverlayNodes(payload, media, mediaBox),
      ],
    },
  ];
}

function iconBarNode(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  zone: "Top" | "Center" | "Bottom",
  iconBar: IconBarDesignContract,
  box: RenderableBox,
) {
  const slotFieldIds = {
    inline: {
      Top: "component.media.inlineTopIconBar.editor",
      Center: "component.media.inlineCenterIconBar.editor",
      Bottom: "component.media.inlineBottomIconBar.editor",
    },
    fullframe: {
      Top: "component.media.fullScreenTopIconBar.editor",
      Center: "component.media.fullScreenCenterIconBar.editor",
      Bottom: "component.media.fullScreenBottomIconBar.editor",
    },
  } as const;
  return renderAuthoringSlot(
    payload,
    "component.media",
    slotFieldIds[media.displayState][zone],
    "component.iconBar",
    "component.iconBar.edgePadding",
    (slotPayload) => iconBarComponentToRenderableAt(slotPayload, iconBar, box),
  );
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
    renderAuthoringSlot(
      payload,
      "component.media",
      media.playbackState === "playing"
        ? "component.media.playText.label.editor"
        : "component.media.idleText.label.editor",
      "component.label",
      "component.label.dimensionMode",
      (slotPayload) => labelComponentToRenderableAt(slotPayload, overlay.label, box),
    ),
  ];
}
