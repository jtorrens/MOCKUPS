import type { SurfaceStyleContract } from "./previewComponentContracts.js";
import type { RuntimeValueBinding } from "./runtimePreviewDocumentContract.js";

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
  tintPaletteColor: string;
  tintAmount: number;
  borderAlpha: number;
  tail: SurfaceTailContract;
  surface: SurfaceStyleContract;
}

export const surfaceRuntimeValueBindings = [
  { fieldId: "tintPaletteColor", runtimeJsonKey: "tintPaletteColor", configPath: ["surface", "tintPaletteColor"] },
  { fieldId: "tintAmount", runtimeJsonKey: "tintAmount", configPath: ["surface", "tintAmount"] },
] as const satisfies readonly RuntimeValueBinding[];
