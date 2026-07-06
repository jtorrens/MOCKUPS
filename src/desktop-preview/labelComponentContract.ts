import type { SurfaceStyleContract } from "./previewComponentContracts.js";

export interface LabelDesignContract {
  id: string;
  text: string;
  subtext: string;
  dimensionMode: "content" | "fixed";
  size: { width: number; height: number };
  padding: { x: number; y: number };
  backgroundColorToken: string;
  surfaceAlpha: number;
  textColorToken: string;
  textSizeToken: string;
  textStyle: "normal" | "italic";
  textAlign: "left" | "center" | "right";
  textGap: number;
  subtextColorToken: string;
  subtextSizeToken: string;
  subtextStyle: "normal" | "italic";
  surface: SurfaceStyleContract;
}
