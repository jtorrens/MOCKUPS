import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import type { AudioDesignContract } from "./audioComponentContract.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { resolveButtonIconComponentFromRecords } from "./buttonIconComponentResolver.js";
import {
  asRecord,
  optionalNumber,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredPlacement,
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
  const audio = asRecord(config.audio);
  const surfaceSlot = asRecord(audio.surfaceSlot);
  const avatarSlot = asRecord(audio.avatarSlot);
  const badgeSlot = asRecord(audio.badgeSlot);
  const showAvatar = requiredBoolean(
    avatarSlot,
    "showAvatar",
    "component.audio.avatar.showAvatar",
  );
  const showBadge = requiredBoolean(
    badgeSlot,
    "showBadge",
    "component.audio.badge.showBadge",
  );
  const avatarConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "avatar", avatarSlot.presetId),
    asRecord(avatarSlot.overrides),
  );
  const badgeConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "buttonIcon", badgeSlot.presetId),
    asRecord(badgeSlot.overrides),
  );
  const surfaceConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "surface", surfaceSlot.presetId),
    asRecord(surfaceSlot.overrides),
  );
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
    optionalNumber(inputs, "currentTimeSeconds", 0),
    durationSeconds,
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
      durationText: formatDuration(durationSeconds - currentTimeSeconds),
    },
    padding: toSpacingPair(requiredStringPair(audio, "padding", "component.audio.padding")),
    textSize: requiredNumber(audio, "textSize", "component.audio.textSize"),
    textColorToken: requiredString(
      audio,
      "textColorToken",
      "component.audio.textColorToken",
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
            inputs,
            componentBaseConfigs,
            "component.audio.avatar",
          )
        : undefined,
    },
    badgeSlot: {
      showBadge,
      iconToken: requiredString(
        badgeSlot,
        "iconToken",
        "component.audio.badge.iconToken",
      ),
      backgroundColor: requiredString(
        badgeSlot,
        "backgroundColor",
        "component.audio.badge.backgroundColor",
      ),
      iconColor: requiredString(
        badgeSlot,
        "iconColor",
        "component.audio.badge.iconColor",
      ),
      placement: requiredPlacement(
        badgeSlot,
        "placement",
        "component.audio.badge.placement",
      ),
      badge: showBadge
        ? {
            ...resolveButtonIconComponentFromRecords(
              badgeConfig,
              inputs,
              componentBaseConfigs,
              "component.audio.badge",
            ),
            iconToken: requiredString(
              badgeSlot,
              "iconToken",
              "component.audio.badge.iconToken",
            ),
            backgroundPaletteColor: requiredString(
              badgeSlot,
              "backgroundColor",
              "component.audio.badge.backgroundColor",
            ),
            iconPaletteColor: requiredString(
              badgeSlot,
              "iconColor",
              "component.audio.badge.iconColor",
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

function normalizePlaybackTime(seconds: number, durationSeconds: number) {
  if (durationSeconds <= 0) return 0;
  const normalized = seconds % durationSeconds;
  return normalized < 0 ? normalized + durationSeconds : normalized;
}
