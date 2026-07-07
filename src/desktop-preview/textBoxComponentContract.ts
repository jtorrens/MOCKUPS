import type {
  SpacingPairContract,
  TypographyStyleContract,
} from "./previewComponentContracts.js";
import type { CursorDesignContract } from "./cursorComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export interface TextBoxDesignContract {
  id: string;
  dimensionMode: "fixed" | "content";
  size: { width: number; height: number };
  padding: SpacingPairContract;
  text: string;
  placeholder: string;
  textColorToken: string;
  placeholderColorToken: string;
  typography: TypographyStyleContract;
  textAlign: "left" | "center" | "right";
  overflowMode: "clip" | "scroll";
  cursorVisible: boolean;
  surface: SurfaceDesignContract;
  cursor: CursorDesignContract;
}
