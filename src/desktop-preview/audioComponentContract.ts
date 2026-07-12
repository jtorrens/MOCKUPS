import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { ButtonDesignContract } from "./buttonComponentContract.js";
import type {
  AlignmentPlacementContract,
  SpacingPairContract,
} from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface AudioDesignContract {
  id: string;
  availableWidth: number;
  playback: {
    durationSeconds: number;
    currentTimeSeconds: number;
    progress: number;
    durationText: string;
  };
  padding: SpacingPairContract;
  textSize: number;
  textColorToken: string;
  playCircleSize: number;
  playIconPaddingToken: string;
  playColorToken: string;
  playIconColorToken: string;
  waveformColorToken: string;
  waveformPlayedColorToken: string;
  waveformBarCount: number;
  waveformGapToken: string;
  waveformMinHeight: number;
  waveformMaxHeight: number;
  progressKnobSize: number;
  surface: SurfaceDesignContract;
  avatarSlot: {
    showAvatar: boolean;
    placement: AlignmentPlacementContract;
    avatar?: AvatarDesignContract;
  };
  badgeSlot: {
    showBadge: boolean;
    iconToken: string;
    placement: AlignmentPlacementContract;
    size: number;
    badge?: ButtonDesignContract;
  };
}
