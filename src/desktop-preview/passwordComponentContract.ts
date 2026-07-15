import type { CodeIndicatorDesignContract, CodeIndicatorState } from "./codeIndicatorComponentContract.js";
import type { IconBarDesignContract } from "./iconBarComponentContract.js";
import type { KeypadDesignContract } from "./keypadComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";

export type PasswordState = CodeIndicatorState;
export type PasswordVerticalAnchor = "container" | "keypad";

export interface PasswordDesignContract {
  id: "component.password";
  state: PasswordState;
  durationFrames: number;
  filledCount: number;
  pushedKey: string;
  upperAnchor: PasswordVerticalAnchor;
  lowerAnchor: PasswordVerticalAnchor;
  labelIndicatorGapToken: string;
  startGapToken: string;
  upperGapToken: string;
  lowerGapToken: string;
  endGapToken: string;
  label: LabelDesignContract;
  indicator: CodeIndicatorDesignContract;
  keypad: KeypadDesignContract;
  iconBar: IconBarDesignContract;
}
