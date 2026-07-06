import type { SurfaceStyleContract } from "./previewComponentContracts.js";

export interface TextInputBarDesignContract {
  id: string;
  height: number;
  text: string;
  placeholder: string;
  backgroundColorToken: string;
  backgroundAlpha: number;
  idleTextColorToken: string;
  textSizeToken: string;
  cursorColorToken: string;
  cursorWidth: number;
  cursorBlinkFrames: number;
  surface: SurfaceStyleContract;
}
