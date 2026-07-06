import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import { mergeComponentDefaults } from "./componentPreviewDefaults.js";
import type { AvatarDesignContract } from "./avatarComponentResolver.js";
import { resolveAvatarComponentFromRecords } from "./avatarComponentResolver.js";
import type { ButtonIconDesignContract } from "./buttonIconComponentResolver.js";
import { resolveButtonIconComponentFromRecords } from "./buttonIconComponentResolver.js";
import {
  asRecord,
  parseObject,
  requiredBoolean,
  requiredNumber,
  requiredPlacement,
  requiredRecord,
  requiredString,
  resolveSurfaceStyle,
  type AlignmentPlacementContract,
  type SurfaceStyleContract,
} from "./componentResolverCommon.js";

export interface AudioDesignContract {
  id: string;
  playback: {
    durationSeconds: number;
    currentTimeSeconds: number;
    progress: number;
    durationText: string;
  };
  padding: { x: number; y: number };
  backgroundColorToken: string;
  backgroundAlpha: number;
  textSize: number;
  textColorToken: string;
  playCircleSize: number;
  playIconPadding: number;
  playColorToken: string;
  playIconColorToken: string;
  waveformColorToken: string;
  waveformPlayedColorToken: string;
  waveformBarCount: number;
  waveformBarWidth: number;
  waveformGap: number;
  waveformMinHeight: number;
  waveformMaxHeight: number;
  progressKnobSize: number;
  surface: SurfaceStyleContract;
  avatarSlot: {
    showAvatar: boolean;
    placement: AlignmentPlacementContract;
    avatar?: AvatarDesignContract;
  };
  badgeSlot: {
    showBadge: boolean;
    iconToken: string;
    backgroundColor: string;
    iconColor: string;
    placement: AlignmentPlacementContract;
    badge?: ButtonIconDesignContract;
  };
}

export function resolveAudioComponent(
  payload: DesignPreviewPayload,
): AudioDesignContract {
  const config = parseObject(payload.configJson);
  const preview = parseObject(payload.designPreviewJson);
  const componentBaseConfigs = parseObject(payload.componentBaseConfigsJson);
  const audio = asRecord(config.audio);
  const style = asRecord(config.style);
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
    requiredRecord(componentBaseConfigs, "avatar", "componentBaseConfigs.avatar"),
    asRecord(avatarSlot.overrides),
  );
  const badgeConfig = mergeComponentDefaults(
    requiredRecord(
      componentBaseConfigs,
      "buttonIcon",
      "componentBaseConfigs.buttonIcon",
    ),
    asRecord(badgeSlot.overrides),
  );
  const durationSeconds = Math.max(
    1,
    Math.round(
      requiredNumber(
        preview,
        "durationSeconds",
        "component.audio.preview.durationSeconds",
      ),
    ),
  );
  const currentTimeSeconds = Math.max(
    0,
    Math.min(
      durationSeconds,
      requiredNumber(
        preview,
        "currentTimeSeconds",
        "component.audio.preview.currentTimeSeconds",
      ),
    ),
  );

  return {
    id: "component.audio",
    playback: {
      durationSeconds,
      currentTimeSeconds,
      progress: currentTimeSeconds / durationSeconds,
      durationText: formatDuration(durationSeconds - currentTimeSeconds),
    },
    padding: parsePair(requiredString(audio, "padding", "component.audio.padding")),
    backgroundColorToken: requiredString(
      audio,
      "backgroundColorToken",
      "component.audio.backgroundColorToken",
    ),
    backgroundAlpha: requiredNumber(
      audio,
      "backgroundAlpha",
      "component.audio.backgroundAlpha",
    ),
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
    playIconPadding: requiredNumber(
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
    waveformBarWidth: requiredNumber(
      audio,
      "waveformBarWidth",
      "component.audio.waveformBarWidth",
    ),
    waveformGap: requiredNumber(
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
    surface: resolveSurfaceStyle(style),
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
            preview,
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
              preview,
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

function formatDuration(totalSeconds: number) {
  const seconds = Math.max(0, Math.round(totalSeconds));
  const minutes = Math.floor(seconds / 60);
  const remainder = seconds % 60;
  return `${minutes}:${remainder.toString().padStart(2, "0")}`;
}

function parsePair(value: string) {
  const [xRaw, yRaw] = value.split("|", 2);
  const x = Number(xRaw?.replace(",", "."));
  const y = Number(yRaw?.replace(",", "."));
  if (!Number.isFinite(x) || !Number.isFinite(y)) {
    throw new Error(`Invalid component.audio numeric pair ${value}`);
  }

  return { x, y };
}
