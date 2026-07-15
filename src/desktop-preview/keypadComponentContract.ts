import type { LabelDesignContract } from "./labelComponentContract.js";
import type { SpacingPairContract } from "./previewComponentContracts.js";

export type KeypadSizingMode = "content" | "fill";
export type KeypadKeyKind = "text" | "icon" | "spacer";
export type KeypadKeyState = "normal" | "active" | "pushed" | "disabled";

export interface KeypadKeyContract {
  id: string;
  kind: KeypadKeyKind;
  value: string;
  text: string;
  subtext: string;
  iconToken: string;
  state: KeypadKeyState;
  backgroundColorToken?: string;
  textColorToken?: string;
  backgroundAlpha?: number;
  borderAlpha?: number;
  label?: LabelDesignContract;
}

export interface KeypadDesignContract {
  id: "component.keypad";
  sizingMode: KeypadSizingMode;
  availableWidth: number;
  columns: number;
  keySize: { width: number; height: number };
  padding: SpacingPairContract;
  columnGapToken: string;
  rowGapToken: string;
  iconSizeToken: string;
  keys: KeypadKeyContract[];
}
