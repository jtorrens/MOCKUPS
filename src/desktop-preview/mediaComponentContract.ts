import type { RenderableBox } from "../visual/renderable/types.js";
import type { IconBarDesignContract } from "./iconBarComponentContract.js";
import type {
  ComponentMotionContract,
  ComponentMotionFrameContract,
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
  topIconBar: IconBarDesignContract;
  centerIconBar: IconBarDesignContract;
  bottomIconBar: IconBarDesignContract;
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
