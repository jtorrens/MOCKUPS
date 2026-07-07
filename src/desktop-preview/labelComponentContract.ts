import type { SpacingPairContract } from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface TypographyStyleContract {
  fontFamilyId: string;
  weight: string;
  style: string;
  sizeToken: string;
  lineHeight: number | string;
}

export interface LabelDesignContract {
  id: string;
  text: string;
  subtext: string;
  dimensionMode: "content" | "fixed";
  size: { width: number; height: number };
  padding: SpacingPairContract;
  textColorToken: string;
  textTypography: TypographyStyleContract;
  textAlign: "left" | "center" | "right";
  textGap: number;
  subtextColorToken: string;
  subtextTypography: TypographyStyleContract;
  surface: SurfaceDesignContract;
}
