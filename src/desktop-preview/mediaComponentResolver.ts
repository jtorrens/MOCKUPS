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
  MediaTextOverlayContract,
  MediaTextOverlayMode,
} from "./mediaComponentContract.js";
import {
  asRecord,
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredNumberPair,
  requiredPlacement,
  requiredString,
  requiredStringPair,
  requiredTypographyStyle,
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
  const fullScreenTransition = requiredBoolean(
    preview,
    "fullScreenTransition",
    "component.media.input.fullScreenTransition",
  );
  const playbackState: MediaPlaybackState = isPlaying ? "playing" : "idle";
  const displayState: MediaDisplayState = isFullScreen ? "fullframe" : "inline";
  const currentTimeSeconds = requiredNumber(
    preview,
    "currentTimeSeconds",
    "component.media.input.currentTimeSeconds",
  );
  const durationSeconds = Math.max(
    0,
    requiredNumber(preview, "durationSeconds", "component.media.input.durationSeconds"),
  );
  const controlBarHeight = Math.max(
    1,
    requiredNumber(media, "controlBarHeight", "component.media.controlBarHeight"),
  );
  const iconBarPadding = requiredStringPair(
    media,
    "iconBarPadding",
    "component.media.iconBarPadding",
  );
  const iconBarState = playbackState === "playing" ? "active" : "idle";
  const iconColorTokenOverride = optionalString(media, "iconColorTokenOverride") || undefined;
  const topIconBar = resolveMediaIconBar(
    media,
    displayState === "fullframe" ? "fullScreenTopIconBarSlot" : "inlineTopIconBarSlot",
    {
      state: iconBarState,
      size: `${viewportSize.first}|${controlBarHeight}`,
      iconColorTokenOverride,
    },
    componentBaseConfigs,
    "component.media.topIconBar",
  );
  const centerIconBar = resolveMediaIconBar(
    media,
    displayState === "fullframe" ? "fullScreenCenterIconBarSlot" : "inlineCenterIconBarSlot",
    {
      state: iconBarState,
      size: `${viewportSize.first}|${viewportSize.second}`,
      iconColorTokenOverride,
    },
    componentBaseConfigs,
    "component.media.centerIconBar",
  );
  const bottomIconBar = resolveMediaIconBar(
    media,
    displayState === "fullframe" ? "fullScreenBottomIconBarSlot" : "inlineBottomIconBarSlot",
    {
      state: iconBarState,
      size: `${viewportSize.first}|${controlBarHeight}`,
      iconColorTokenOverride,
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
    currentTimeSeconds,
    durationSeconds,
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
    iconBarPadding: {
      xToken: iconBarPadding.first,
      yToken: iconBarPadding.second,
    },
    topIconBar,
    centerIconBar,
    bottomIconBar,
    textOverlay: resolveMediaTextOverlay(
      media,
      playbackState === "playing" ? "playText" : "idleText",
      playbackState,
      currentTimeSeconds,
      durationSeconds,
    ),
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
      trigger: fullScreenTransition && isFullScreen,
      timeSeconds: optionalNumber(preview, "motionTimeSeconds", 0),
    },
  };
}

function resolveMediaTextOverlay(
  media: Record<string, unknown>,
  key: string,
  playbackState: MediaPlaybackState,
  currentTimeSeconds: number,
  durationSeconds: number,
): MediaTextOverlayContract | null {
  const overlay = asRecord(media[key]);
  const enabled = requiredBoolean(overlay, "enabled", `component.media.${key}.enabled`);
  const mode = mediaTextOverlayMode(
    requiredString(overlay, "mode", `component.media.${key}.mode`),
  );
  const text = optionalString(overlay, "text");
  const targetSeconds = Math.max(
    0,
    requiredNumber(overlay, "targetSeconds", `component.media.${key}.targetSeconds`),
  );
  const resolvedText = mediaOverlayText(
    mode,
    text,
    targetSeconds,
    currentTimeSeconds,
    durationSeconds,
  );

  return {
    id: `component.media.${playbackState}.text`,
    enabled,
    mode,
    text,
    resolvedText,
    targetSeconds,
    textColorToken: requiredString(
      overlay,
      "textColorToken",
      `component.media.${key}.textColorToken`,
    ),
    typography: requiredTypographyStyle(
      overlay,
      "typography",
      `component.media.${key}.typography`,
    ),
    placement: requiredPlacement(
      overlay,
      "placement",
      `component.media.${key}.placement`,
    ),
    textAlign: mediaTextAlign(
      requiredString(overlay, "textAlign", `component.media.${key}.textAlign`),
    ),
  };
}

function mediaOverlayText(
  mode: MediaTextOverlayMode,
  text: string,
  targetSeconds: number,
  currentTimeSeconds: number,
  durationSeconds: number,
) {
  if (mode === "free") return text;
  const target = targetSeconds > 0 ? targetSeconds : durationSeconds;
  const seconds = mode === "countUp"
    ? Math.min(target, Math.max(0, currentTimeSeconds))
    : Math.max(0, target - Math.max(0, currentTimeSeconds));
  return formatClock(seconds);
}

function formatClock(value: number) {
  const totalSeconds = Math.max(0, Math.round(value));
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  return `${minutes}:${seconds.toString().padStart(2, "0")}`;
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

function mediaTextOverlayMode(value: string): MediaTextOverlayMode {
  if (value === "free" || value === "countUp" || value === "countDown") return value;
  throw new Error(`Unsupported media text overlay mode ${value}`);
}

function mediaTextAlign(value: string): "left" | "center" | "right" {
  if (value === "left" || value === "center" || value === "right") return value;
  throw new Error(`Unsupported media text overlay alignment ${value}`);
}
