import type { RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";

export type ComponentCollectionSizingMode = "fill" | "content";
export type ComponentCollectionAlignment = "start" | "center" | "end";
export type ComponentCollectionGapMode = "fixed" | "reflow";

export interface ComponentCollectionItemContract {
  id: string;
  componentType: string;
  presetReference: string;
  config: Record<string, unknown>;
  alignment: ComponentCollectionAlignment;
  gapBeforeMode: ComponentCollectionGapMode;
  gapBeforeToken: string;
  gapBeforeWeight: number;
  inputs: Record<string, unknown>;
}

export type ComponentCollectionChildRenderer = (payload: DesignPreviewPayload) => RenderableNode;
