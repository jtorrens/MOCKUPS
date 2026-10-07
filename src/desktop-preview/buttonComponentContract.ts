import type { SpacingPairContract } from "./previewComponentContracts.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";
import type { BadgeDesignContract } from "./badgeComponentContract.js";
import type { RuntimeValueBinding, RuntimeValueLink } from "./runtimePreviewDocumentContract.js";

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
export const buttonRuntimeValueBindings = [
  {
    fieldId: "surfaceTintPaletteColor", runtimeJsonKey: "surfaceTintPaletteColor", configPath: ["surface", "tintPaletteColor"],
    boundaries: [{ fieldId: "component.button.appearance.surfaceSlot", slotPath: ["button", "appearance", "surfaceSlot"], componentType: "surface" }],
  },
  {
    fieldId: "surfaceTintAmount", runtimeJsonKey: "surfaceTintAmount", configPath: ["surface", "tintAmount"],
    boundaries: [{ fieldId: "component.button.appearance.surfaceSlot", slotPath: ["button", "appearance", "surfaceSlot"], componentType: "surface" }],
  },
  { fieldId: "iconToken", runtimeJsonKey: "iconToken", configPath: ["button", "iconToken"] },
  { fieldId: "iconColorToken", runtimeJsonKey: "iconColorToken", configPath: ["button", "appearance", "iconColorToken"] },
  {
    fieldId: "textColorToken", runtimeJsonKey: "textColorToken", configPath: ["label", "textColorToken"],
    boundaries: [{ fieldId: "component.button.appearance.labelSlot", slotPath: ["button", "appearance", "labelSlot"], componentType: "label" }],
  },
  {
    fieldId: "textSizeToken", runtimeJsonKey: "textSizeToken", configPath: ["label", "textTypography", "sizeToken"],
    boundaries: [{ fieldId: "component.button.appearance.labelSlot", slotPath: ["button", "appearance", "labelSlot"], componentType: "label" }],
  },
] as const satisfies readonly RuntimeValueBinding[];

export const buttonLabelValueLinks = [
  { sourceFieldId: "textColorToken", sourceJsonKey: "textColorToken", targetFieldId: "textColorToken", targetJsonKey: "textColorToken" },
  { sourceFieldId: "textSizeToken", sourceJsonKey: "textSizeToken", targetFieldId: "textSizeToken", targetJsonKey: "textSizeToken" },
] as const satisfies readonly RuntimeValueLink[];

export const buttonSurfaceValueLinks = [
  { sourceFieldId: "surfaceTintPaletteColor", sourceJsonKey: "surfaceTintPaletteColor", targetFieldId: "tintPaletteColor", targetJsonKey: "tintPaletteColor" },
  { sourceFieldId: "surfaceTintAmount", sourceJsonKey: "surfaceTintAmount", targetFieldId: "tintAmount", targetJsonKey: "tintAmount" },
] as const satisfies readonly RuntimeValueLink[];
