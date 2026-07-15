export type FingerprintState = "initial" | "active" | "correct" | "incorrect";

export interface FingerprintDesignContract {
  id: string;
  state: FingerprintState;
  progress: number;
  size: { width: number; height: number };
  iconToken: string;
  iconSizeToken: string;
  iconSizeMultiplier: number;
  scanLineThickness: number;
  colorToken: string;
}
