export interface RenderableBox {
  x: number;
  y: number;
  width: number;
  height: number;
}

export interface RenderableTransform {
  x?: number;
  y?: number;
  scale?: number;
  rotation?: number;
  opacity?: number;
}

export interface RenderableAsset {
  type: string;
  uri: string;
}

export interface RenderableNode {
  id: string;
  type: string;
  frame?: number;
  box?: RenderableBox;
  transform?: RenderableTransform;
  style?: Record<string, unknown>;
  text?: string;
  asset?: RenderableAsset;
  children?: RenderableNode[];
  metadata?: Record<string, unknown>;
}
