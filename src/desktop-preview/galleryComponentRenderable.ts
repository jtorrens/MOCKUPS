import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import { numberToken, renderScale } from "./componentRenderableCommon.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { GalleryDesignContract } from "./galleryComponentContract.js";
import { mediaComponentToRenderableAt } from "./mediaComponentRenderable.js";
import { renderAuthoringSlot } from "./previewAuthoringTarget.js";
import { surfaceComponentToRenderableAt } from "./surfaceComponentRenderable.js";

export function galleryComponentToRenderable(
  payload: DesignPreviewPayload,
  gallery: GalleryDesignContract,
): RenderableNode {
  const screen = payload.previewFrame;
  return galleryComponentToRenderableAt(payload, gallery, {
    x: screen.screenX,
    y: screen.screenY,
    width: screen.screenWidth,
    height: screen.screenHeight,
  });
}

export function galleryComponentToRenderableAt(
  payload: DesignPreviewPayload,
  gallery: GalleryDesignContract,
  available: RenderableBox,
): RenderableNode {
  const scale = renderScale(payload);
  const containerPaddingX = numberToken(payload, gallery.containerPaddingXToken) * scale;
  const containerPaddingY = numberToken(payload, gallery.containerPaddingYToken) * scale;
  const paddingX = numberToken(payload, gallery.paddingXToken) * scale;
  const paddingY = numberToken(payload, gallery.paddingYToken) * scale;
  const carouselHeight = gallery.itemHeight * gallery.selectedScale * scale
    + (containerPaddingY + paddingY) * 2;
  const root = sizedBox(
    available,
    gallery.widthMode === "fill" ? available.width : gallery.width * scale,
    gallery.mode === "carousel"
      ? carouselHeight
      : gallery.heightMode === "fill"
        ? available.height
        : gallery.height * scale,
  );
  const surface = insetBox(root, containerPaddingX, containerPaddingY);
  const content = insetBox(surface, paddingX, paddingY);
  const gap = numberToken(payload, gallery.gapToken) * scale;
  const itemWidth = gallery.itemWidth * scale;
  const itemHeight = gallery.itemHeight * scale;
  const items = gallery.mode === "carousel"
    ? carouselItems(payload, gallery, content, itemWidth, itemHeight, gap)
    : gridItems(payload, gallery, content, itemWidth, itemHeight, gap);
  return {
    id: gallery.id,
    type: "group",
    frame: 0,
    box: root,
    style: { overflow: "visible" },
    children: [
      renderAuthoringSlot(
        payload,
        "component.gallery",
        "component.gallery.surface.editor",
        "component.surface",
        "component.surface.backgroundColorToken",
        (slotPayload) => surfaceComponentToRenderableAt(slotPayload, gallery.surface, surface),
      ),
      edgeFade(
        `${gallery.id}.fade`,
        content,
        gallery.mode === "carousel" ? "horizontal" : "vertical",
        gallery.fadeExtent * scale,
        items,
      ),
    ],
  };
}

function carouselItems(
  payload: DesignPreviewPayload,
  gallery: GalleryDesignContract,
  content: RenderableBox,
  itemWidth: number,
  itemHeight: number,
  gap: number,
) {
  const selected = Math.min(gallery.selectedIndex, Math.max(0, gallery.items.length - 1));
  const startX = content.x + content.width / 2 - itemWidth / 2 - selected * (itemWidth + gap);
  return itemGroup(
    payload,
    gallery,
    content,
    (index) => ({
      x: startX + index * (itemWidth + gap),
      y: content.y + (content.height - itemHeight) / 2,
      width: itemWidth,
      height: itemHeight,
    }),
    (index) => 1 + (gallery.selectedScale - 1)
      * Math.max(0, 1 - Math.abs(index - selected)),
  );
}

function gridItems(
  payload: DesignPreviewPayload,
  gallery: GalleryDesignContract,
  content: RenderableBox,
  itemWidth: number,
  itemHeight: number,
  gap: number,
) {
  const columns = Math.max(1, Math.floor((content.width + gap) / (itemWidth + gap)));
  const gridWidth = columns * itemWidth + Math.max(0, columns - 1) * gap;
  const gridStartX = content.x + (content.width - gridWidth) / 2;
  const rowStep = itemHeight + gap;
  return itemGroup(
    payload,
    gallery,
    content,
    (index) => {
      const row = Math.floor(index / columns);
      const column = index % columns;
      return {
        x: gridStartX + column * (itemWidth + gap),
        y: content.y + (row - gallery.scrollRow) * rowStep,
        width: itemWidth,
        height: itemHeight,
      };
    },
    (index) => gallery.items[index]?.selected ? gallery.selectedScale : 1,
  );
}

function itemGroup(
  payload: DesignPreviewPayload,
  gallery: GalleryDesignContract,
  content: RenderableBox,
  boxAt: (index: number) => RenderableBox,
  scaleAt: (index: number) => number,
): RenderableNode {
  return {
    id: `${gallery.id}.items`,
    type: "group",
    frame: 0,
    box: content,
    style: { overflow: "visible" },
    children: gallery.items.map((item, index) => {
      const itemBox = boxAt(index);
      const media = renderAuthoringSlot(
        payload,
        "component.gallery",
        "component.gallery.media.editor",
        "component.media",
        "component.media.surface.editor",
        (slotPayload) => mediaComponentToRenderableAt(slotPayload, item.media, itemBox),
      );
      const itemScale = scaleAt(index);
      const scaledMedia = Math.abs(itemScale - 1) > 0.000001
        ? {
            ...media,
            transform: { ...media.transform, scale: itemScale },
            style: { ...media.style, transformOrigin: "center", zIndex: 2 },
          }
        : media;
      return scaledMedia;
    }),
  };
}

function edgeFade(
  id: string,
  box: RenderableBox,
  axis: "horizontal" | "vertical",
  extent: number,
  child: RenderableNode,
): RenderableNode {
  if (extent <= 0) {
    return {
      id,
      type: "group",
      frame: 0,
      box,
      style: { overflow: "hidden" },
      children: [child],
    };
  }
  const length = axis === "horizontal" ? box.width : box.height;
  const fade = Math.min(Math.max(0, extent), length / 2);
  const origin = axis === "horizontal" ? box.x : box.y;
  const endMask: RenderableNode = {
    id: `${id}.end`,
    type: "group",
    frame: 0,
    box,
    style: {
      opacityMask: {
        axis,
        start: origin + length - fade,
        end: origin + length,
        beforeOpacity: 1,
        afterOpacity: 0,
      },
    },
    children: [child],
  };
  return {
    id: `${id}.start`,
    type: "group",
    frame: 0,
    box,
    style: {
      overflow: "hidden",
      opacityMask: {
        axis,
        start: origin,
        end: origin + fade,
        beforeOpacity: 0,
        afterOpacity: 1,
      },
    },
    children: [endMask],
  };
}

function sizedBox(available: RenderableBox, width: number, height: number): RenderableBox {
  return {
    x: available.x + (available.width - width) / 2,
    y: available.y + (available.height - height) / 2,
    width,
    height,
  };
}

function insetBox(box: RenderableBox, x: number, y: number): RenderableBox {
  return {
    x: box.x + x,
    y: box.y + y,
    width: Math.max(1, box.width - x * 2),
    height: Math.max(1, box.height - y * 2),
  };
}
