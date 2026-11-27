import type { AlignmentPlacementContract } from "./previewComponentContracts.js";

export interface ReactionEmitterDesignContract {
  size: number;
  riseHeightPercent: number;
  dispersionWidth: number;
  placement: AlignmentPlacementContract;
  particles: Array<{
    id: string;
    glyph: string;
    rise: number;
    drift: number;
    scale: number;
    opacity: number;
  }>;
}
