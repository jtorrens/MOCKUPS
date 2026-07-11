import type { SpacingPairContract } from "./previewComponentContracts.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export type ButtonContentMode = "icon" | "text" | "iconText";
export type ButtonState = "normal" | "active" | "pushed" | "disabled";

export interface ButtonStateDesignContract {
  opacity: number;
  iconColorToken: string;
  label?: LabelDesignContract;
  surface: SurfaceDesignContract;
}

export interface ButtonDesignContract {
  id: string;
  contentMode: ButtonContentMode;
  state: ButtonState;
  dimensionMode: "content" | "fixed";
  size: { width: number; height: number };
  padding: SpacingPairContract;
  contentGapToken: string;
  iconToken: string;
  iconSizeToken: string;
  pushedDurationToken: string;
  stateStyle: ButtonStateDesignContract;
}
