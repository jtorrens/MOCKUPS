import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { ButtonIconDesignContract } from "./buttonIconComponentContract.js";
import type {
  AlignmentPlacementContract,
  SurfaceStyleContract,
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
