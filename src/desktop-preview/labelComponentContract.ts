import type {
  AlignmentPlacementContract,
  SpacingPairContract,
  TypographyStyleContract,
} from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface LabelDesignContract {
  id: string;
  text: string;
  subtext: string;
  textSizeMultiplier: number;
  subtextSizeMultiplier: number;
  dimensionMode: "content" | "fixed";
  size: { width: number; height: number };
  padding: SpacingPairContract;
  textColorToken: string;
  textTypography: TypographyStyleContract;
  textAlign: "left" | "center" | "right";
  textGapToken: string;
  reserveSubtextSpace: boolean;
  subtextPlacement: AlignmentPlacementContract;
  subtextColorToken: string;
  subtextTypography: TypographyStyleContract;
  surface: SurfaceDesignContract;
}
