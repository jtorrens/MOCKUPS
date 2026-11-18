import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  embeddedComponentConfig,
} from "./runtimePreviewDocumentContract.js";
import type { AudioDesignContract } from "./audioComponentContract.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { resolveBadgeComponentFromRecords } from "./badgeComponentResolver.js";
import { literalLabelPreview, resolveLabelComponentFromRecords, staticLabelFrameContext } from "./labelComponentResolver.js";
import {
  optionalString,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredPlacement,
  requiredPossiblyEmptyString,
  requiredRecord,
  requiredString,
  requiredStringPair,
} from "./componentResolverCommon.js";
import { resolveSurfaceComponentAtSize } from "./surfaceComponentResolver.js";

export function resolveAudioComponent(
  payload: DesignPreviewPayload,
): AudioDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  return resolveAudioComponentFromRecords(
    config,
    preview,
    componentBaseConfigs,
    "component.audio",
  );
}

export function resolveAudioComponentFromRecords(
  config: Record<string, unknown>,
  inputs: Record<string, unknown>,
  componentBaseConfigs: Record<string, unknown>,
  id: string,
): AudioDesignContract {
  const audio = requiredRecord(config, "audio", "component.audio");
  const surfaceSlot = requiredRecord(audio, "surfaceSlot", "component.audio.surfaceSlot");
  const avatarSlot = requiredRecord(audio, "avatarSlot", "component.audio.avatarSlot");
  const badgeSlot = requiredRecord(audio, "badgeSlot", "component.audio.badgeSlot");
  const avatarComponentSlot = requiredRecord(
    avatarSlot,
    "componentSlot",
    "component.audio.avatarSlot.componentSlot",
  );
  const badgeComponentSlot = requiredRecord(
    badgeSlot,
    "componentSlot",
    "component.audio.badgeSlot.componentSlot",
  );
  const durationLabelSlot = requiredRecord(audio, "durationLabelSlot", "component.audio.durationLabelSlot");
  const showAvatar = requiredBoolean(
    avatarSlot,
    "showAvatar",
    "component.audio.avatar.showAvatar",
  );
  const showBadge = requiredBoolean(
    badgeSlot,
    "showBadge",
    "component.audio.badgeSlot.showBadge",
  );
  const badgeInputs = {
    contentMode: requiredString(badgeSlot, "contentMode", "component.audio.badgeSlot.contentMode"),
    iconToken: requiredPossiblyEmptyString(badgeSlot, "iconToken", "component.audio.badgeSlot.iconToken"),
    text: requiredPossiblyEmptyString(badgeSlot, "text", "component.audio.badgeSlot.text"),
    size: requiredNumber(badgeSlot, "size", "component.audio.badgeSlot.size"),
    backgroundPaletteColor: requiredString(badgeSlot, "backgroundPaletteColor", "component.audio.badgeSlot.backgroundPaletteColor"),
    contentPaletteColor: requiredString(badgeSlot, "contentPaletteColor", "component.audio.badgeSlot.contentPaletteColor"),
  };
  if (badgeInputs.contentMode !== "icon" && badgeInputs.contentMode !== "text") {
    throw new Error(`Unsupported Audio badge contentMode ${badgeInputs.contentMode}`);
  }
  const contentKey = badgeInputs.contentMode === "icon" ? "iconToken" : "text";
  if (!badgeInputs[contentKey].trim()) {
    throw new Error(`component.audio.badgeSlot.${contentKey} must not be empty`);
  }
  if (!Number.isInteger(badgeInputs.size) || badgeInputs.size < 1 || badgeInputs.size > 512) {
    throw new Error("component.audio.badgeSlot.size must be an integer between 1 and 512");
  }
  const avatarConfig = embeddedComponentConfig(componentBaseConfigs, avatarComponentSlot, "avatar", "component.audio.avatarSlot.componentSlot");
  const badgeConfig = embeddedComponentConfig(componentBaseConfigs, badgeComponentSlot, "badge", "component.audio.badgeSlot.componentSlot");
  const surfaceConfig = embeddedComponentConfig(componentBaseConfigs, surfaceSlot, "surface", "component.audio.surfaceSlot");
  const durationLabelConfig = embeddedComponentConfig(componentBaseConfigs, durationLabelSlot, "label", "component.audio.durationLabelSlot");
  const durationSeconds = Math.max(
    1,
    Math.round(
      requiredNumber(
        inputs,
        "durationSeconds",
        "component.audio.preview.durationSeconds",
      ),
    ),
  );
  const currentTimeSeconds = normalizePlaybackTime(
    requiredNumber(
      inputs,
      "currentTimeSeconds",
      "component.audio.input.currentTimeSeconds",
    ),
    durationSeconds,
    optionalString(inputs, "playbackMode") === "loop",
  );
  const availableWidth = Math.max(
    1,
    requiredNumber(inputs, "availableWidth", "component.audio.input.availableWidth"),
  );

  return {
    id,
    availableWidth,
    playback: {
      durationSeconds,
      currentTimeSeconds,
      progress: currentTimeSeconds / durationSeconds,
    },
    padding: toSpacingPair(requiredStringPair(audio, "padding", "component.audio.padding")),
    durationLabel: resolveLabelComponentFromRecords(
      durationLabelConfig,
      literalLabelPreview(formatDuration(durationSeconds - currentTimeSeconds)),
      componentBaseConfigs,
      `${id}.durationLabel`,
      staticLabelFrameContext,
    ),
    playCircleSize: requiredNumber(
      audio,
      "playCircleSize",
      "component.audio.playCircleSize",
    ),
    playIconPaddingToken: requiredString(
      audio,
      "playIconPadding",
      "component.audio.playIconPadding",
    ),
    playColorToken: requiredString(
      audio,
      "playColorToken",
      "component.audio.playColorToken",
    ),
    playIconColorToken: requiredString(
      audio,
      "playIconColorToken",
      "component.audio.playIconColorToken",
    ),
    waveformColorToken: requiredString(
      audio,
      "waveformColorToken",
      "component.audio.waveformColorToken",
    ),
    waveformPlayedColorToken: requiredString(
      audio,
      "waveformPlayedColorToken",
      "component.audio.waveformPlayedColorToken",
    ),
    waveformBarCount: requiredNumber(
      audio,
      "waveformBarCount",
      "component.audio.waveformBarCount",
    ),
    waveformGapToken: requiredString(
      audio,
      "waveformGap",
      "component.audio.waveformGap",
    ),
    waveformMinHeight: requiredNumber(
      audio,
      "waveformMinHeight",
      "component.audio.waveformMinHeight",
    ),
    waveformMaxHeight: requiredNumber(
      audio,
      "waveformMaxHeight",
      "component.audio.waveformMaxHeight",
    ),
    progressKnobSize: requiredNumber(
      audio,
      "progressKnobSize",
      "component.audio.progressKnobSize",
    ),
    surface: resolveSurfaceComponentAtSize(
      surfaceConfig,
      { width: availableWidth, height: 1 },
      "component.audio.surface",
    ),
    avatarSlot: {
      showAvatar,
      placement: requiredPlacement(
        avatarSlot,
        "placement",
        "component.audio.avatar.placement",
      ),
      avatar: showAvatar
        ? resolveAvatarComponentFromRecords(
            avatarConfig,
            { ...inputs, showBadge: false, badgeIconToken: "system_check", badgeText: "1" },
            componentBaseConfigs,
            "component.audio.avatar",
          )
        : undefined,
    },
    badgeSlot: {
      badge: showBadge
        ? {
            ...resolveBadgeComponentFromRecords(
              badgeConfig,
              badgeInputs,
              "component.audio.badge",
            ),
            placement: requiredPlacement(
              badgeSlot,
              "placement",
              "component.audio.badge.placement",
            ),
          }
        : undefined,
    },
  };
}

function toSpacingPair(pair: { first: string; second: string }) {
  return { xToken: pair.first, yToken: pair.second };
}

function formatDuration(totalSeconds: number) {
  const seconds = Math.max(0, Math.round(totalSeconds));
  const minutes = Math.floor(seconds / 60);
  const remainder = seconds % 60;
  return `${minutes}:${remainder.toString().padStart(2, "0")}`;
}

function normalizePlaybackTime(seconds: number, durationSeconds: number, loop: boolean) {
  if (durationSeconds <= 0) return 0;
  if (!loop) return Math.min(durationSeconds, Math.max(0, seconds));
  const normalized = seconds % durationSeconds;
  return normalized < 0 ? normalized + durationSeconds : normalized;
}
