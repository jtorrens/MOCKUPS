import type { ComponentStackLayoutContract } from "./componentStackComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface SurfaceStackDesignContract {
  id: "surfaceStack";
  width: number;
  height: number;
  horizontalPaddingToken: string;
  verticalPaddingToken: string;
  surface: SurfaceDesignContract;
  layout: ComponentStackLayoutContract;
}
