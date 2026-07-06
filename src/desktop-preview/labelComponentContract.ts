import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface LabelDesignContract {
  id: string;
  text: string;
  subtext: string;
  dimensionMode: "content" | "fixed";
  size: { width: number; height: number };
  padding: { x: number; y: number };
  textColorToken: string;
  textSizeToken: string;
  textStyle: "normal" | "italic";
  textAlign: "left" | "center" | "right";
  textGap: number;
  subtextColorToken: string;
  subtextSizeToken: string;
  subtextStyle: "normal" | "italic";
  surface: SurfaceDesignContract;
}
