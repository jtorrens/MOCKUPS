import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { embeddedComponentConfig } from "./componentPreviewDefaults.js";
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
  optionalNumber,
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredNumberPair,
  requiredPlacement,
  requiredRecord,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import { resolveIconBarComponentFromRecords } from "./iconBarComponentResolver.js";
import { literalLabelPreview, resolveLabelComponentFromRecords, staticLabelFrameContext } from "./labelComponentResolver.js";
import { requiredMotionContract } from "./previewMotionHelpers.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

export function resolveMediaComponent(
  payload: DesignPreviewPayload,
): MediaDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  return resolveMediaComponentFromRecords(
    config,
    preview,
    componentBaseConfigs,
    "component.media",
  );
}

export function resolveMediaComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): MediaDesignContract {
  const media = requiredRecord(config, "media", "component.media");
  const viewportSize = requiredNumberPair(
    inputs,
    "viewportSize",
    "component.media.input.viewportSize",
  );
  const mediaOffset = requiredNumberPair(
    inputs,
    "mediaOffset",
    "component.media.input.mediaOffset",
  );
  const isPlaying = requiredBoolean(inputs, "isPlaying", "component.media.input.isPlaying");
  const isFullScreen = requiredBoolean(
    inputs,
    "isFullScreen",
    "component.media.input.isFullScreen",
  );
  const fullScreenTransition = requiredBoolean(
    inputs,
    "fullScreenTransition",
    "component.media.input.fullScreenTransition",
  );
  const playbackState: MediaPlaybackState = isPlaying ? "playing" : "idle";
  const displayState: MediaDisplayState = isFullScreen ? "fullframe" : "inline";
  const currentTimeSeconds = requiredNumber(
    inputs,
    "currentTimeSeconds",
    "component.media.input.currentTimeSeconds",
  );
  const durationSeconds = Math.max(
    0,
    requiredNumber(inputs, "durationSeconds", "component.media.input.durationSeconds"),
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
  const surfaceSlot = requiredRecord(media, "surfaceSlot", "component.media");
  const surfaceConfig = embeddedComponentConfig(
    componentBaseConfigs,
    surfaceSlot,
    "surface",
    "component.media.surfaceSlot",
  );

  return {
    id,
    sourceUri: optionalString(inputs, "mediaSource"),
    mediaKind: mediaKind(requiredString(inputs, "mediaType", "component.media.input.mediaType")),
    playbackState,
    currentTimeSeconds,
    durationSeconds,
    displayState,
    fullframeOrientation: mediaFullframeOrientation(
      requiredString(
        inputs,
        "fullframeOrientation",
        "component.media.input.fullframeOrientation",
      ),
    ),
    viewport: {
      width: Math.max(1, viewportSize.first),
      height: Math.max(1, viewportSize.second),
      scale: Math.max(
        0.01,
        requiredNumber(inputs, "mediaScale", "component.media.input.mediaScale"),
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
      componentBaseConfigs,
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
      requiredNumber(inputs, "controlsElapsedMs", "component.media.input.controlsElapsedMs"),
    ),
    motion: requiredMotionContract(media, "motion", "component.media.motion"),
    motionFrame: {
      trigger: fullScreenTransition,
      elapsedMs: optionalNumber(inputs, "motionElapsedMs", 0),
      reverse: fullScreenTransition && !isFullScreen,
    },
  };
}

function resolveMediaTextOverlay(
  media: Record<string, unknown>,
  key: string,
  playbackState: MediaPlaybackState,
  currentTimeSeconds: number,
  durationSeconds: number,
  componentBaseConfigs: Record<string, unknown>,
): MediaTextOverlayContract | null {
  const overlay = requiredRecord(media, key, "component.media");
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
  const labelSlot = requiredRecord(overlay, "labelSlot", `component.media.${key}`);
  const labelConfig = embeddedComponentConfig(
    componentBaseConfigs,
    labelSlot,
    "label",
    `component.media.${key}.labelSlot`,
  );

  return {
    id: `component.media.${playbackState}.text`,
    enabled,
    mode,
    text,
    targetSeconds,
    placement: requiredPlacement(
      overlay,
      "placement",
      `component.media.${key}.placement`,
    ),
    label: resolveLabelComponentFromRecords(
      labelConfig,
      literalLabelPreview(resolvedText),
      componentBaseConfigs,
      `component.media.${playbackState}.label`,
      staticLabelFrameContext,
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
  const slot = requiredRecord(media, slotKey, "component.media");
  const config = embeddedComponentConfig(
    componentBaseConfigs,
    slot,
    "iconBar",
    `component.media.${slotKey}`,
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
