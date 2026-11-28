export interface ReactionEmitterDesignContract {
  size: number;
  riseHeightPercent: number;
  dispersionWidth: number;
  particles: Array<{
    id: string;
    glyph: string;
    rise: number;
    drift: number;
    scale: number;
    opacity: number;
  }>;
}
