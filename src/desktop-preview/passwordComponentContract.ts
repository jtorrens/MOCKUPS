import type { CodeIndicatorDesignContract, CodeIndicatorState } from "./codeIndicatorComponentContract.js";
import type { IconBarDesignContract } from "./iconBarComponentContract.js";
import type { KeypadDesignContract } from "./keypadComponentContract.js";
import type { LabelDesignContract } from "./labelComponentContract.js";
import type { FingerprintDesignContract } from "./fingerprintComponentContract.js";
import type { FaceRecognitionDesignContract } from "./faceRecognitionComponentContract.js";
import type { DrawPasswordDesignContract } from "./drawPasswordComponentContract.js";

export type PasswordState = CodeIndicatorState;
export type PasswordVerticalAnchor = "container" | "input";
export type PasswordMode = "pin" | "fingerprint" | "faceRecognition" | "drawPassword";
export type PasswordInputContract =
  | { kind: "keypad"; component: KeypadDesignContract }
  | { kind: "fingerprint"; component: FingerprintDesignContract }
  | { kind: "faceRecognition"; component: FaceRecognitionDesignContract }
  | { kind: "drawPassword"; component: DrawPasswordDesignContract };

export interface PasswordDesignContract {
  id: "component.password";
  mode: PasswordMode;
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
  input: PasswordInputContract;
  iconBar: IconBarDesignContract;
}
