import type {
  AlignmentPlacementContract,
} from "./componentResolverCommon.js";
import type { LabelDesignContract } from "./labelComponentContract.js";

export interface AvatarDesignContract {
  id: string;
  size: number;
  cornerRadiusToken: string;
  labelSlot: {
    showLabel: boolean;
    showSubtext: boolean;
    placement: AlignmentPlacementContract;
    label?: LabelDesignContract;
  };
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
