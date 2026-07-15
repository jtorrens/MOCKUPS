import type {
  AlignmentPlacementContract,
} from "./previewComponentContracts.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { BadgeDesignContract } from "./badgeComponentContract.js";

export interface AvatarActorDesignContract {
  id: string;
  displayName: string;
  shortName: string;
  initials: string;
  avatar: {
    imageUri: string;
    backgroundColor: string;
    textColor: string;
    scale: number;
    offsetX: number;
    offsetY: number;
    baseSize: number;
  };
}

export interface AvatarDesignContract {
  id: string;
  actor: AvatarActorDesignContract;
  size: number;
  cornerRadiusToken: string;
  labelSlot: {
    showLabel: boolean;
    showSubtext: boolean;
    placement: AlignmentPlacementContract;
    label?: LabelDesignContract;
  };
  badge?: BadgeDesignContract;
  surface: {
    shadowEnabled: boolean;
    reliefEnabled: boolean;
    borderWidth: number;
    borderColorToken: string;
    reliefAngle: number;
    reliefExtent: number;
    reliefSpread: number;
    reliefTopIntensity: number;
    reliefBottomIntensity: number;
  };
}
