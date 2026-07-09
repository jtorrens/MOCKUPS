import type { RenderableBox } from "../visual/renderable/types.js";
import type { IconBarDesignContract } from "./iconBarComponentContract.js";
import type {
  ComponentMotionContract,
  ComponentMotionFrameContract,
  AlignmentPlacementContract,
  TypographyStyleContract,
} from "./previewComponentContracts.js";
import type { SurfaceDesignContract } from "./surfaceComponentContract.js";

export type MediaKind = "image" | "video";
export type MediaPlaybackState = "idle" | "playing";
export type MediaDisplayState = "inline" | "fullframe";
export type MediaFullframeOrientation = "portrait" | "landscape";

export interface MediaViewportContract {
  width: number;
  height: number;
  scale: number;
  offsetX: number;
  offsetY: number;
}

export interface MediaIconBarPaddingContract {
  xToken: string;
  yToken: string;
}

export type MediaTextOverlayMode = "free" | "countUp" | "countDown";

export interface MediaTextOverlayContract {
  id: string;
  enabled: boolean;
  mode: MediaTextOverlayMode;
  text: string;
  resolvedText: string;
  targetSeconds: number;
  textColorToken: string;
  typography: TypographyStyleContract;
  placement: AlignmentPlacementContract;
  textAlign: "left" | "center" | "right";
}

export interface MediaDesignContract {
  id: string;
  sourceUri: string;
  mediaKind: MediaKind;
  playbackState: MediaPlaybackState;
  currentTimeSeconds: number;
  durationSeconds: number;
  displayState: MediaDisplayState;
  fullframeOrientation: MediaFullframeOrientation;
  viewport: MediaViewportContract;
  surface: SurfaceDesignContract;
  iconBarPadding: MediaIconBarPaddingContract;
  topIconBar: IconBarDesignContract;
  centerIconBar: IconBarDesignContract;
  bottomIconBar: IconBarDesignContract;
  textOverlay: MediaTextOverlayContract | null;
  controlsFadeDelayMs: number;
  controlsFadeDurationMs: number;
  controlsElapsedMs: number;
  motion: ComponentMotionContract;
  motionFrame: ComponentMotionFrameContract;
}

export interface MediaRenderBoxes {
  root: RenderableBox;
  media: RenderableBox;
}
