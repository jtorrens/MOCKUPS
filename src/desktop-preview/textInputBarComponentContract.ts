import type { SpacingPairContract } from "./previewComponentContracts.js";
import type { IconRowDesignContract } from "./iconRowComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface TextInputBarDesignContract {
  id: string;
  height: number;
  barPadding: SpacingPairContract;
  textPadding: SpacingPairContract;
  iconGapToken: string;
  text: string;
  placeholder: string;
  idleTextColorToken: string;
  textSizeToken: string;
  cursorColorToken: string;
  cursorWidth: number;
  cursorBlinkFrames: number;
  barSurface: SurfaceDesignContract;
  surface: SurfaceDesignContract;
  leftIconRow: IconRowDesignContract;
  rightIconRow: IconRowDesignContract;
}
