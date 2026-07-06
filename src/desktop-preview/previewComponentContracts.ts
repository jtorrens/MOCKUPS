export interface AlignmentPlacementContract {
  mode: "center" | "edge";
  alignX: number;
  alignY: number;
  offsetX: number;
  offsetY: number;
}

export interface SurfaceStyleContract {
  shadowEnabled: boolean;
  reliefEnabled: boolean;
  borderWidth: number;
  borderColorToken: string;
  cornerRadiusToken: string;
  reliefAngle: number;
  reliefExtent: number;
  reliefSpread: number;
  reliefTopIntensity: number;
  reliefBottomIntensity: number;
}
