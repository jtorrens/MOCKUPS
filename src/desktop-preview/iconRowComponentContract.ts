import type { ButtonDesignContract } from "./buttonComponentContract.js";
import type { RuntimeValueBinding, RuntimeValueLink } from "./runtimePreviewDocumentContract.js";

export interface IconRowItemContract {
  id: string;
  button: ButtonDesignContract;
}

export interface IconRowDesignContract {
  id: string;
  orientation: "horizontal" | "vertical";
  itemSizingMode: "content" | "fillParent";
  gapToken: string;
  items: IconRowItemContract[];
}
export const iconRowButtonValueLinks = [
  { sourceFieldId: "text", sourceJsonKey: "text", targetFieldId: "sampleText", targetJsonKey: "sampleText" },
  { sourceFieldId: "iconToken", sourceJsonKey: "iconToken", targetFieldId: "iconToken", targetJsonKey: "iconToken" },
  { sourceFieldId: "iconSizeToken", sourceJsonKey: "iconSizeToken", targetFieldId: "iconSizeToken", targetJsonKey: "iconSizeToken" },
  { sourceFieldId: "textSizeToken", sourceJsonKey: "textSizeToken", targetFieldId: "textSizeToken", targetJsonKey: "textSizeToken" },
] as const satisfies readonly RuntimeValueLink[];

export const iconRowRuntimeValueBindings = [
  { fieldId: "orientation", runtimeJsonKey: "orientation", configPath: ["iconRow", "orientation"] },
  { fieldId: "itemSizingMode", runtimeJsonKey: "itemSizingMode", configPath: ["iconRow", "itemSizingMode"] },
  { fieldId: "structuralItems", runtimeJsonKey: "structuralItems", configPath: ["iconRow", "items"] },
  { fieldId: "gap", runtimeJsonKey: "gap", configPath: ["iconRow", "gap"] },
  { fieldId: "iconSizeToken", runtimeJsonKey: "iconSizeToken", configPath: ["iconRow", "iconSizeToken"] },
  { fieldId: "textSizeToken", runtimeJsonKey: "textSizeToken", configPath: ["iconRow", "textSizeToken"] },
] as const satisfies readonly RuntimeValueBinding[];

export const iconRowSharedSizeLinks = [
  { sourceFieldId: "iconSizeToken", sourceJsonKey: "iconSizeToken", targetFieldId: "iconSizeToken", targetJsonKey: "iconSizeToken" },
  { sourceFieldId: "textSizeToken", sourceJsonKey: "textSizeToken", targetFieldId: "textSizeToken", targetJsonKey: "textSizeToken" },
] as const satisfies readonly RuntimeValueLink[];
