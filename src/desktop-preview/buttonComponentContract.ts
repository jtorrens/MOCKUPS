import type { SpacingPairContract } from "./previewComponentContracts.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";
import type { BadgeDesignContract } from "./badgeComponentContract.js";

export type ButtonContentMode = "icon" | "text" | "iconText";

export interface ButtonAppearanceContract {
  iconColorToken: string;
  label?: LabelDesignContract;
  surface: SurfaceDesignContract;
}

export interface ButtonDesignContract {
  id: string;
  contentMode: ButtonContentMode;
  enabled: boolean;
  pressed: boolean;
  opacity: number;
  scale: number;
  dimensionMode: "content" | "fixed";
  size: { width: number; height: number };
  padding: SpacingPairContract;
  contentGapToken: string;
  iconToken: string | null;
  iconSizeToken: string;
  appearance: ButtonAppearanceContract;
  badge?: BadgeDesignContract;
}
