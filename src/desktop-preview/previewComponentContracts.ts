export interface AlignmentPlacementContract {
  mode: "center" | "insideEdge" | "outsideEdge";
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

export interface SpacingPairContract {
  xToken: string;
  yToken: string;
}

export interface TypographyStyleContract {
  fontFamilyId: string;
  weight: string;
  style: string;
  sizeToken: string;
  lineHeight: number | string;
}

export interface IconSlotsContract {
  left: string[];
  center: string[];
  right: string[];
}

export interface ComponentMotionContract {
  transition: "none" | "slide" | "swipe" | "scale";
  direction: "top" | "bottom" | "left" | "right";
  bounds: "parent" | "screen";
  fade: boolean;
  translate: boolean;
  scale: boolean;
}

export interface ComponentMotionFrameContract {
  trigger: boolean;
  elapsedMs: number;
}
