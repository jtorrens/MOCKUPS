import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export type ComponentStackSizingMode = "fill" | "content";
export type ComponentStackAlignment = "start" | "center" | "end";
export type ComponentStackGapMode = "fixed" | "reflow";

export interface ComponentStackItemContract {
  id: string;
  componentType: string;
  presetReference: string;
  config: Record<string, unknown>;
  alignment: ComponentStackAlignment;
  gapBeforeMode: ComponentStackGapMode;
  gapBeforeToken: string;
  gapBeforeWeight: number;
  inputs: Record<string, unknown>;
}

export interface ComponentStackDesignContract {
  id: "componentStack";
  sizingMode: ComponentStackSizingMode;
  startGapToken: string;
  endGapToken: string;
  items: ComponentStackItemContract[];
}

export type ComponentStackChildRenderer = (payload: DesignPreviewPayload) => RenderableNode;
