import type {
  IconSlotsContract,
  SurfaceStyleContract,
} from "./previewComponentContracts.js";

export interface KeyboardDesignContract {
  id: string;
  backgroundColorToken: string;
  backgroundAlpha: number;
  keyBackgroundColorToken: string;
  keyTextColorToken: string;
  bottomIconColorToken: string;
  keyPaddingToken: string;
  keyCornerRadius: number;
  keyShadowEnabled: boolean;
  pressedEffect: "popup" | "scale" | "none";
  specialKeyTextScale: number;
  emojiScale: number;
  bottomIconSlots: IconSlotsContract;
  surface: SurfaceStyleContract;
}
