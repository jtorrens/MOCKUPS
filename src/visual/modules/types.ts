import type { RenderableNode } from "../renderable/types.js";

export interface VisualModule<InputProps> {
  type: string;
  version: number;
  render(input: InputProps, context?: unknown): RenderableNode;
}

export interface VisualModuleContext {
  frame: number;
  fps: number;
}
