import type {
  ComponentMotionContract,
  ComponentMotionFrameContract,
  SurfaceStyleContract,
  TypographyStyleContract,
} from "./previewComponentContracts.js";
import type { IconBarDesignContract } from "./iconBarComponentContract.js";

export type KeyboardMode = "lowercase" | "shift" | "numeric" | "symbols" | "emoji";

export interface KeyboardKeyContract {
  id: string;
  label: string;
  kind: "character" | "special" | "space" | "emoji";
  weight: number;
}

export interface KeyboardDesignContract {
  id: string;
  language: string;
  mode: KeyboardMode;
  pressedKey: string;
  backgroundColorToken: string;
  backgroundAlpha: number;
  keyBackgroundColorToken: string;
  specialKeyBackgroundColorToken: string;
  pressedKeyBackgroundColorToken: string;
  keyTextColorToken: string;
  heightToken: string;
  keyGapToken: string;
  rowGapToken: string;
  keyPaddingToken: string;
  keyCornerRadiusToken: string;
  keyBorderColorToken: string;
  keyBorderWidth: number;
  keyShadowEnabled: boolean;
  pressedEffect: "popup" | "scale" | "none";
  specialKeyTextScale: number;
  emojiScale: number;
  typography: TypographyStyleContract;
  rows: KeyboardKeyContract[][];
  iconRowPlacement: "top" | "bottom";
  iconRowsHeight: number;
  iconEdgePaddingToken: string;
  iconBar: IconBarDesignContract;
  surface: SurfaceStyleContract;
  motion: ComponentMotionContract;
  motionFrame: ComponentMotionFrameContract;
}
