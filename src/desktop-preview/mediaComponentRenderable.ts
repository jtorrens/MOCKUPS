import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import {
  boundedCenterBox,
  numberToken,
  previewScreenBox,
  renderScale,
} from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { iconBarComponentToRenderableAt } from "./iconBarComponentRenderable.js";
import type { IconBarDesignContract } from "./iconBarComponentContract.js";
import type { MediaDesignContract, MediaRenderBoxes } from "./mediaComponentContract.js";
import { mediaUriForPath } from "./previewAssetResolver.js";
import { wrapMotionFrame } from "./previewMotionHelpers.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function mediaComponentToRenderable(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
): RenderableNode {
  const boxes = mediaBoxes(payload, media);
  const mediaSurfaceNode = surfaceComponentToRenderableAt(payload, media.surface, boxes.media);
  const mediaContentNode = mediaContent(payload, media, boxes.media);
  const controlNodes = mediaControlNodes(payload, media, boxes.media);
  const children = [
    ...mediaBars(payload, media, boxes),
    mediaSurfaceNode,
    mediaContentClipNode(payload, media, boxes.media, mediaContentNode),
    ...controlNodes,
  ];
  const node = {
    id: media.id,
    type: "group",
    frame: 0,
    box: boxes.root,
    style: {
      overflow: "visible",
    },
    children,
  } satisfies RenderableNode;
  const motionBounds = media.motion.bounds === "screen"
    ? previewScreenBox(payload)
    : boxes.root;
  return wrapMotionFrame(
    payload,
    node,
    media.motion,
    media.motionFrame,
    boxes.root,
    motionBounds,
  );
}

function mediaContentClipNode(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
  box: RenderableBox,
  content: RenderableNode,
): RenderableNode {
  return {
    id: `${media.id}.contentClip`,
    type: "group",
    frame: 0,
    box,
    style: {
      borderRadius: numberToken(payload, media.surface.surface.cornerRadiusToken) * renderScale(payload),
      overflow: "hidden",
    },
    children: [content],
  };
}

function mediaBoxes(
  payload: DesignPreviewPayload,
  media: MediaDesignContract,
): MediaRenderBoxes {
  const scale = renderScale(payload);
  if (media.displayState === "fullframe") {
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

  const width = media.viewport.width * scale;
  const height = media.viewport.height * scale;
  const mediaBox = boundedCenterBox(payload, width, height);
  return {
    root: mediaBox,
    media: mediaBox,
  };
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
  const bars: RenderableNode[] = [
    {
      id: `${media.id}.fullframeBackground`,
      type: "surface",
      frame: 0,
      box: boxes.root,
      style: {
        background: "#000000",
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
  const uri = mediaUriForPath(payload, media.sourceUri);
  if (uri && media.mediaKind === "image") {
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

  return mediaPlaceholder(media, box, uri ? "Video frame pending" : "No media source");
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
  const topHeight = media.topIconBar.size.height * renderScale(payload);
  const bottomHeight = media.bottomIconBar.size.height * renderScale(payload);
  const controlsBox = mediaBox;
  const topBox = {
    x: mediaBox.x,
    y: mediaBox.y,
    width: mediaBox.width,
    height: topHeight,
  };
  const centerBox = {
    x: mediaBox.x,
    y: mediaBox.y,
    width: mediaBox.width,
    height: mediaBox.height,
  };
  const bottomBox = {
    x: mediaBox.x,
    y: mediaBox.y + mediaBox.height - bottomHeight,
    width: mediaBox.width,
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
