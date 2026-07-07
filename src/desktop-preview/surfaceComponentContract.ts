import type { SurfaceStyleContract } from "./previewComponentContracts.js";

export interface SurfaceTailContract {
  enabled: boolean;
  style: "rounded_wedge" | "curved_hook" | "simple_triangle" | "cut_corner";
  side: "left" | "right";
  vertical: "top" | "bottom";
  width: number;
  height: number;
  outerCornerRadius: number;
}

export interface SurfaceDesignContract {
  id: string;
  width: number;
  height: number;
  backgroundColorToken: string;
  backgroundAlpha: number;
  borderAlpha: number;
  tail: SurfaceTailContract;
  surface: SurfaceStyleContract;
}
