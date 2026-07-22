import type { RenderableBox, RenderableNode } from "../visual/renderable/types.js";
import type { DesignPreviewPayload } from "./designPreviewPayload.js";
import type { ComponentMotionContract } from "./previewComponentContracts.js";

export type ComponentCollectionSizingMode = "fill" | "content";
export type ComponentCollectionAlignment = "start" | "center" | "end";
export type ComponentCollectionGapMode = "fixed" | "reflow";

export interface ComponentCollectionLayoutItem {
  id: string;
  alignment: ComponentCollectionAlignment;
  gapBeforeMode: ComponentCollectionGapMode;
  gapBeforeToken: string;
  gapBeforeWeight: number;
}

export interface ComponentCollectionItemContract extends ComponentCollectionLayoutItem {
  componentType: string;
  variantReference: string;
  config: Record<string, unknown>;
  inputs: Record<string, unknown>;
  present: boolean;
  presenceMotion: ComponentMotionContract;
  activationFrame?: number;
  exitFrame?: number;
  reflowStartFrame?: number;
  reflowFromInputs?: Record<string, unknown>;
  presenceTransition?: boolean;
  presenceElapsedMs?: number;
}

export type ComponentCollectionChildRenderer = (payload: DesignPreviewPayload, assignedBox?: RenderableBox) => RenderableNode;
