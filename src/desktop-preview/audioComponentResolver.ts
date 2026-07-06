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
  type AlignmentPlacementContract,
} from "./componentResolverCommon.js";

export interface AudioDesignContract {
  id: string;
  size: { width: number; height: number };
  textSize: number;
  playColorToken: string;
  waveformColorToken: string;
  knobSize: number;
  avatarSlot: {
    showAvatar: boolean;
    placement: AlignmentPlacementContract;
    avatar?: AvatarDesignContract;
  };
  badgeSlot: {
    showBadge: boolean;
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

  return {
    id: "component.audio",
    size: parseSize(requiredString(audio, "size", "component.audio.size")),
    textSize: requiredNumber(audio, "textSize", "component.audio.textSize"),
    playColorToken: requiredString(
      audio,
      "playColorToken",
      "component.audio.playColorToken",
    ),
    waveformColorToken: requiredString(
      audio,
      "waveformColorToken",
      "component.audio.waveformColorToken",
    ),
    knobSize: requiredNumber(audio, "knobSize", "component.audio.knobSize"),
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
      placement: requiredPlacement(
        badgeSlot,
        "placement",
        "component.audio.badge.placement",
      ),
      badge: showBadge
        ? resolveButtonIconComponentFromRecords(
            badgeConfig,
            preview,
            componentBaseConfigs,
            "component.audio.badge",
          )
        : undefined,
    },
  };
}

function parseSize(value: string) {
  const [widthRaw, heightRaw] = value.split("|", 2);
  const width = Number(widthRaw?.replace(",", "."));
  const height = Number(heightRaw?.replace(",", "."));
  if (!Number.isFinite(width) || !Number.isFinite(height)) {
    throw new Error(`Invalid component.audio.size ${value}`);
  }

  return { width, height };
}
