import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export type CodeIndicatorState = "initial" | "correct" | "incorrect";

export interface CodeIndicatorDesignContract {
  id: "component.codeIndicator" | string;
  count: number;
  filledCount: number;
  state: CodeIndicatorState;
  displayMode: "visible" | "collapsed";
  glyphSize: { width: number; height: number };
  gapToken: string;
  emptySurface: SurfaceDesignContract;
  filledSurface: SurfaceDesignContract;
}
