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

export type RenderableNodeType =
  | "group"
  | "icon"
  | "image"
  | "path"
  | "surface"
  | "text";

export interface RenderableFontFace {
  family: string;
  uri: string;
  weight?: number | string;
  style?: string;
}

export interface RenderableMetadata {
  fontFaces?: RenderableFontFace[];
  fallbackText?: string;
  imageBaseSize?: number;
  imageOffsetX?: number;
  imageOffsetY?: number;
  imageScale?: number;
}

export interface RenderableNode {
  id: string;
  type: RenderableNodeType;
  frame?: number;
  box?: RenderableBox;
  transform?: RenderableTransform;
  style?: Record<string, unknown>;
  text?: string;
  asset?: RenderableAsset;
  children?: RenderableNode[];
  metadata?: RenderableMetadata;
}
