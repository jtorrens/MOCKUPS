import type {
  AlignmentPlacementContract,
} from "./previewComponentContracts.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface ButtonIconDesignContract {
  id: string;
  sizeMode: "fixed" | "iconSize";
  buttonSize: number;
  iconSizeToken: string;
  iconToken: string;
  iconPaddingToken: string;
  iconColorToken: string;
  backgroundPaletteColor?: string;
  iconPaletteColor?: string;
  labelSlot: {
    showLabel: boolean;
    showSubtext: boolean;
    placement: AlignmentPlacementContract;
    label?: LabelDesignContract;
  };
  surface: SurfaceDesignContract;
}
