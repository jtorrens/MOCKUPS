import type { SurfaceStyleContract } from "./previewComponentContracts.js";

export interface SurfaceDesignContract {
  id: string;
  width: number;
  height: number;
  backgroundColorToken: string;
  backgroundAlpha: number;
  borderAlpha: number;
  surface: SurfaceStyleContract;
}
