import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface TextInputBarDesignContract {
  id: string;
  height: number;
  text: string;
  placeholder: string;
  idleTextColorToken: string;
  textSizeToken: string;
  cursorColorToken: string;
  cursorWidth: number;
  cursorBlinkFrames: number;
  surface: SurfaceDesignContract;
}
