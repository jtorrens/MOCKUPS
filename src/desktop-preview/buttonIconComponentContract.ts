import type {
  AlignmentPlacementContract,
  SurfaceStyleContract,
} from "./previewComponentContracts.js";
import type { LabelDesignContract } from "./labelComponentContract.js";

export interface ButtonIconDesignContract {
  id: string;
  buttonSize: number;
  iconSize: number;
  iconToken: string;
  iconPadding: number;
  backgroundColorToken: string;
  backgroundAlpha: number;
  iconColorToken: string;
  backgroundPaletteColor?: string;
  iconPaletteColor?: string;
  labelSlot: {
    showLabel: boolean;
    showSubtext: boolean;
    placement: AlignmentPlacementContract;
    label?: LabelDesignContract;
  };
  surface: SurfaceStyleContract;
}
