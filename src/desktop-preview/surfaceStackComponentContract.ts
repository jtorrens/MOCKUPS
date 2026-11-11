import type { ComponentStackLayoutContract } from "./componentStackComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface SurfaceStackDesignContract {
  accent: import("./visualAccent.js").VisualAccentFrame;
  id: "surfaceStack";
  width: number;
  height: number;
  horizontalPaddingToken: string;
  verticalPaddingToken: string;
  surface: SurfaceDesignContract;
  layout: ComponentStackLayoutContract;
}
