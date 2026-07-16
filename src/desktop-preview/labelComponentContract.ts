import type { SpacingPairContract, TypographyStyleContract } from "./previewComponentContracts.js";
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
  textShadowEnabled: boolean;
  textColorToken: string;
  textTypography: TypographyStyleContract;
  textAlign: "left" | "center" | "right";
  textGapToken: string;
  reserveSubtextSpace: boolean;
  subtextVerticalPosition: "top" | "bottom";
  subtextHorizontalAlign: "left" | "center" | "right";
  subtextColorToken: string;
  subtextTypography: TypographyStyleContract;
  surface: SurfaceDesignContract;
}
