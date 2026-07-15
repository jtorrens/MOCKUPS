import type { AlignmentPlacementContract, TypographyStyleContract } from "./previewComponentContracts.js";

export interface BadgeDesignContract {
  id: string;
  contentMode: "icon" | "text";
  iconToken: string;
  text: string;
  size: number;
  textTypography: TypographyStyleContract;
  backgroundPaletteColor: string;
  contentPaletteColor: string;
  paddingToken: string;
  placement: AlignmentPlacementContract;
}
