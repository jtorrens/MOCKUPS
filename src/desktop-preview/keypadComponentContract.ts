import type { LabelDesignContract } from "./labelComponentContract.js";
import type { SpacingPairContract } from "./previewComponentContracts.js";

export type KeypadSizingMode = "content" | "fill";
export type KeypadKeyKind = "key" | "spacer";
export type KeypadKeyState = "normal" | "active" | "disabled";

export interface KeypadKeyContract {
  id: string;
  kind: KeypadKeyKind;
  value: string;
  text: string;
  subtext: string;
  state: KeypadKeyState;
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
  keys: KeypadKeyContract[];
}
