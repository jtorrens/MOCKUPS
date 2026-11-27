export interface ReactionEmitterDesignContract {
  size: number;
  height: number;
  spread: number;
  particles: Array<{
    id: string;
    glyph: string;
    rise: number;
    drift: number;
    scale: number;
    opacity: number;
  }>;
}
