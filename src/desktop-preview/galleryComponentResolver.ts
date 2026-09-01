import path from "node:path";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
import {
  parseObject,
  requiredNumber,
  requiredNumberPair,
  requiredRecord,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import type {
  GalleryDesignContract,
  GalleryItemContract,
  GalleryMode,
  GallerySizeMode,
} from "./galleryComponentContract.js";
import { resolveMediaComponentFromRecords } from "./mediaComponentResolver.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

export function resolveGalleryComponent(payload: DesignPreviewPayload): GalleryDesignContract {
  const config = parseObject(payload.configJson);
  const inputs = parseObject(payload.designPreviewJson);
  const baseConfigs = parseObject(payload.componentBaseConfigsJson);
  const gallery = requiredRecord(config, "gallery", "component.gallery");
  const mode = galleryMode(requiredString(gallery, "mode", "component.gallery.mode"));
  const viewport = requiredNumberPair(inputs, "viewportSize", "component.gallery.viewportSize");
  const ratio = requiredStringPair(gallery, "aspectRatio", "component.gallery.aspectRatio");
  const ratioWidth = positiveRatioPart(ratio.first, "width");
  const ratioHeight = positiveRatioPart(ratio.second, "height");
  const itemHeight = Math.max(1, requiredNumber(gallery, "itemHeight", "component.gallery.itemHeight"));
  const itemWidth = itemHeight * ratioWidth / ratioHeight;
  const containerPadding = requiredStringPair(
    gallery,
    "containerPadding",
    "component.gallery.containerPadding",
  );
  const padding = requiredStringPair(gallery, "padding", "component.gallery.padding");
  const selectedIndex = Math.max(0, requiredNumber(
    inputs,
    "selectedIndex",
    "component.gallery.selectedIndex",
  ));
  const scrollRow = Math.max(0, requiredNumber(inputs, "scrollRow", "component.gallery.scrollRow"));
  const sources = galleryMediaSources(
    payload.projectMediaFiles ?? [],
    requiredString(inputs, "mediaDirectory", "component.gallery.mediaDirectory"),
    payload.projectMediaRoot ?? "",
  );
  const surfaceConfig = embeddedComponentConfig(
    baseConfigs,
    requiredRecord(gallery, "surfaceSlot", "component.gallery"),
    "surface",
    "component.gallery.surfaceSlot",
  );
  const normalMediaConfig = embeddedComponentConfig(
    baseConfigs,
    requiredRecord(gallery, "mediaSlot", "component.gallery"),
    "media",
    "component.gallery.mediaSlot",
  );
  const effectiveSelectedIndex = Math.min(
    Math.round(selectedIndex),
    Math.max(0, sources.length - 1),
  );
  const items = sources.map((sourceUri, index): GalleryItemContract => {
    const selected = index === effectiveSelectedIndex;
    const id = stableMediaId(sourceUri);
    return {
      id,
      sourceUri,
      selected,
      media: resolveMediaComponentFromRecords(
        payload,
        normalMediaConfig,
        mediaInputs(sourceUri, itemWidth, itemHeight),
        baseConfigs,
        `component.gallery.item.${id}`,
      ),
    };
  });
  return {
    id: "gallery",
    mode,
    widthMode: gallerySizeMode(
      requiredString(gallery, "widthMode", "component.gallery.widthMode"),
      "width",
    ),
    heightMode: gallerySizeMode(
      requiredString(gallery, "heightMode", "component.gallery.heightMode"),
      "height",
    ),
    width: Math.max(1, viewport.first),
    height: Math.max(1, viewport.second),
    itemWidth,
    itemHeight,
    gapToken: requiredString(gallery, "gapToken", "component.gallery.gapToken"),
    containerPaddingXToken: containerPadding.first,
    containerPaddingYToken: containerPadding.second,
    paddingXToken: padding.first,
    paddingYToken: padding.second,
    fadeExtent: Math.max(0, requiredNumber(gallery, "fadeExtent", "component.gallery.fadeExtent")),
    selectedScale: Math.max(0.01, requiredNumber(gallery, "selectedScale", "component.gallery.selectedScale")),
    selectedIndex,
    scrollRow,
    surface: resolveSurfaceComponentAtSize(
      surfaceConfig,
      { width: Math.max(1, viewport.first), height: Math.max(1, viewport.second) },
      "component.gallery.surface",
    ),
    items,
  };
}

export function galleryMediaSources(
  files: string[],
  directoryValue: string,
  mediaRoot: string,
) {
  return directorySources(files, normalizeDirectory(directoryValue, mediaRoot));
}

function gallerySizeMode(value: string, axis: string): GallerySizeMode {
  if (value === "fixed" || value === "fill") return value;
  throw new Error(`Unsupported Gallery ${axis} mode ${value}`);
}

function galleryMode(value: string): GalleryMode {
  if (value === "carousel" || value === "gallery") return value;
  throw new Error(`Unsupported gallery mode ${value}`);
}

function positiveRatioPart(value: string, label: string) {
  const result = Number(value);
  if (!Number.isFinite(result) || result <= 0) {
    throw new Error(`component.gallery.aspectRatio ${label} must be positive`);
  }
  return result;
}

function normalizeDirectory(value: string, mediaRoot: string) {
  const trimmed = value.trim();
  const relative = path.isAbsolute(trimmed) && mediaRoot
    ? path.relative(mediaRoot, trimmed)
    : trimmed;
  return relative.replace(/\\/g, "/").replace(/^\/+|\/+$/g, "");
}

function directorySources(files: string[], directory: string) {
  const prefix = directory ? `${directory}/` : "";
  return files
    .map((file) => file.replace(/\\/g, "/").replace(/^\/+/, ""))
    .filter((file) => file.startsWith(prefix) && !file.slice(prefix.length).includes("/"))
    .sort((left, right) => left.localeCompare(right, undefined, { numeric: true, sensitivity: "base" }));
}

function stableMediaId(sourceUri: string) {
  return sourceUri.replace(/[^a-zA-Z0-9_-]+/g, "_");
}

function mediaInputs(sourceUri: string, width: number, height: number) {
  const mediaType = /\.(mp4|mov|m4v|webm)$/i.test(sourceUri) ? "video" : "image";
  return {
    mediaSource: sourceUri,
    mediaType,
    viewportSize: `${Math.max(1, Math.round(width))}|${Math.max(1, Math.round(height))}`,
    mediaScale: 1,
    mediaOffset: "0|0",
    currentTimeSeconds: 0,
    durationSeconds: 0,
    fullframeOrientation: height >= width ? "portrait" : "landscape",
    controlsElapsedMs: 0,
    isPlaying: false,
    isFullScreen: false,
    fullScreenTransition: false,
    motionElapsedMs: 0,
  };
}
