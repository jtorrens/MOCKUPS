export type FaceRecognitionState = "initial" | "active" | "correct" | "incorrect";

export interface FaceRecognitionDesignContract {
  id: string;
  state: FaceRecognitionState;
  progress: number;
  size: { width: number; height: number };
  iconToken: string;
  iconSizeToken: string;
  iconSizeMultiplier: number;
  strokeWidth: number;
  colorToken: string;
}
