import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import type {
  MediaDesignContract,
  MediaDisplayState,
  MediaFullframeOrientation,
  MediaKind,
  MediaPlaybackState,
} from "./mediaComponentContract.js";
import {
  asRecord,
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredNumberPair,
  requiredString,
} from "./componentResolverCommon.js";
import { resolveIconBarComponentFromRecords } from "./iconBarComponentResolver.js";
import { requiredMotionContract } from "./previewMotionHelpers.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

export function resolveMediaComponent(
  payload: DesignPreviewPayload,
): MediaDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const media = asRecord(config.media);
  const viewportSize = requiredNumberPair(
    preview,
    "viewportSize",
    "component.media.input.viewportSize",
  );
  const mediaOffset = requiredNumberPair(
    preview,
    "mediaOffset",
    "component.media.input.mediaOffset",
  );
  const isPlaying = requiredBoolean(preview, "isPlaying", "component.media.input.isPlaying");
  const isFullScreen = requiredBoolean(
    preview,
    "isFullScreen",
    "component.media.input.isFullScreen",
  );
  const playbackState: MediaPlaybackState = isPlaying ? "playing" : "idle";
  const displayState: MediaDisplayState = isFullScreen ? "fullframe" : "inline";
  const controlBarHeight = Math.max(
    1,
    requiredNumber(media, "controlBarHeight", "component.media.controlBarHeight"),
  );
  const iconBarState = playbackState === "playing" ? "active" : "idle";
  const topIconBar = resolveMediaIconBar(
    media,
    "topIconBarSlot",
    {
      state: iconBarState,
      size: `${viewportSize.first}|${controlBarHeight}`,
    },
    componentBaseConfigs,
    "component.media.topIconBar",
  );
  const centerIconBar = resolveMediaIconBar(
    media,
    "centerIconBarSlot",
    {
      state: iconBarState,
      size: `${viewportSize.first}|${viewportSize.second}`,
    },
    componentBaseConfigs,
    "component.media.centerIconBar",
  );
  const bottomIconBar = resolveMediaIconBar(
    media,
    "bottomIconBarSlot",
    {
      state: iconBarState,
      size: `${viewportSize.first}|${controlBarHeight}`,
    },
    componentBaseConfigs,
    "component.media.bottomIconBar",
  );
  const surfaceSlot = asRecord(media.surfaceSlot);
  const surfaceConfig = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "surface",
      requiredString(surfaceSlot, "presetId", "component.media.surfaceSlot.presetId"),
    ),
    asRecord(surfaceSlot.overrides),
  );

  return {
    id: "component.media",
    sourceUri: optionalString(preview, "mediaSource"),
    mediaKind: mediaKind(requiredString(preview, "mediaType", "component.media.input.mediaType")),
    playbackState,
    currentTimeSeconds: requiredNumber(
      preview,
      "currentTimeSeconds",
      "component.media.input.currentTimeSeconds",
    ),
    durationSeconds: Math.max(
      0,
      requiredNumber(preview, "durationSeconds", "component.media.input.durationSeconds"),
    ),
    displayState,
    fullframeOrientation: mediaFullframeOrientation(
      requiredString(
        preview,
        "fullframeOrientation",
        "component.media.input.fullframeOrientation",
      ),
    ),
    viewport: {
      width: Math.max(1, viewportSize.first),
      height: Math.max(1, viewportSize.second),
      scale: Math.max(
        0.01,
        requiredNumber(preview, "mediaScale", "component.media.input.mediaScale"),
      ),
      offsetX: mediaOffset.first,
      offsetY: mediaOffset.second,
    },
    surface: resolveSurfaceComponentAtSize(
      surfaceConfig,
      {
        width: Math.max(1, viewportSize.first),
        height: Math.max(1, viewportSize.second),
      },
      "component.media.surface",
    ),
    topIconBar,
    centerIconBar,
    bottomIconBar,
    controlsFadeDelayMs: Math.max(
      0,
      requiredNumber(media, "controlsFadeDelayMs", "component.media.controlsFadeDelayMs"),
    ),
    controlsFadeDurationMs: Math.max(
      0,
      requiredNumber(media, "controlsFadeDurationMs", "component.media.controlsFadeDurationMs"),
    ),
    controlsElapsedMs: Math.max(
      0,
      requiredNumber(preview, "controlsElapsedMs", "component.media.input.controlsElapsedMs"),
    ),
    motion: requiredMotionContract(media, "motion", "component.media.motion"),
    motionFrame: {
      trigger: isPlaying && isFullScreen,
      timeSeconds: optionalNumber(preview, "motionTimeSeconds", 0),
    },
  };
}

function resolveMediaIconBar(
  media: Record<string, unknown>,
  slotKey: string,
  inputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
) {
  const slot = asRecord(media[slotKey]);
  const config = mergeComponentDefaults(
    componentPresetConfig(
      componentBaseConfigs,
      "iconBar",
      requiredString(slot, "presetId", `component.media.${slotKey}.presetId`),
    ),
    asRecord(slot.overrides),
  );
  return resolveIconBarComponentFromRecords(config, inputs, componentBaseConfigs, id);
}

function mediaKind(value: string): MediaKind {
  if (value === "image" || value === "video") return value;
  throw new Error(`Unsupported media type ${value}`);
}

function mediaFullframeOrientation(value: string): MediaFullframeOrientation {
  if (value === "portrait" || value === "landscape") return value;
  throw new Error(`Unsupported media fullframe orientation ${value}`);
}
