import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import {
  componentPresetConfig,
  mergeComponentDefaults,
} from "./componentPreviewDefaults.js";
import type { AudioDesignContract } from "./audioComponentContract.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import { resolveButtonComponentFromRecords } from "./buttonComponentResolver.js";
import { resolveLabelComponentFromRecords } from "./labelComponentResolver.js";
import {
  asRecord,
  optionalNumber,
  optionalString,
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
  const durationLabelSlot = asRecord(audio.durationLabelSlot);
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
    componentPresetConfig(componentBaseConfigs, "button", badgeSlot.presetId),
    asRecord(badgeSlot.overrides),
  );
  const surfaceConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "surface", surfaceSlot.presetId),
    asRecord(surfaceSlot.overrides),
  );
  const durationLabelConfig = mergeComponentDefaults(
    componentPresetConfig(componentBaseConfigs, "label", durationLabelSlot.presetId),
    asRecord(durationLabelSlot.overrides),
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
      { sampleText: formatDuration(durationSeconds - currentTimeSeconds), sampleSubtext: "" },
      componentBaseConfigs,
      `${id}.durationLabel`,
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
      placement: requiredPlacement(
        badgeSlot,
        "placement",
        "component.audio.badge.placement",
      ),
      size: requiredNumber(badgeSlot, "size", "component.audio.badge.size"),
      badge: showBadge
        ? resolveButtonComponentFromRecords(
              badgeConfig,
              {
                contentMode: "icon",
                state: "normal",
                iconToken: requiredString(badgeSlot, "iconToken", "component.audio.badge.iconToken"),
                iconSizeToken: "theme.iconSizes.s",
                textSizeToken: "theme.typography.sizes.s",
                sampleText: "",
                pushTrigger: false,
                pushElapsedMs: 0,
              },
              componentBaseConfigs,
              "component.audio.badge",
            )
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
