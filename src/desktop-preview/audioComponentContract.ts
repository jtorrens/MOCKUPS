import type { AvatarDesignContract } from "./avatarComponentContract.js";
import type { BadgeDesignContract } from "./badgeComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
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
  };
  padding: SpacingPairContract;
  durationLabel: LabelDesignContract;
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
    badge?: BadgeDesignContract;
  };
}
