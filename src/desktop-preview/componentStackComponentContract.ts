import type {
  ComponentCollectionChildRenderer,
  ComponentCollectionGapMode,
  ComponentCollectionItemContract,
  ComponentCollectionSizingMode,
} from "./componentCollectionContract.js";
import type {
  AlignmentPlacementContract,
  ComponentMotionContract,
  ComponentMotionFrameContract,
} from "./previewComponentContracts.js";

export type ComponentStackSizingMode = ComponentCollectionSizingMode;
export type ComponentStackGapMode = ComponentCollectionGapMode;

export interface ComponentStackAlternativeContract {
  id: string;
  component?: ComponentCollectionItemContract;
  placement: AlignmentPlacementContract;
  behavior: "replace" | "overlay";
  active: boolean;
  isDefault: boolean;
  enterMotion: ComponentMotionContract;
  exitMotion: ComponentMotionContract;
  localFrame: number;
  enterMotionFrame?: ComponentMotionFrameContract;
  exitMotionFrame?: ComponentMotionFrameContract;
  activationFrame?: number;
  exitFrame?: number;
}

export interface ComponentStackSlotContract {
  id: string;
  sizeMode: "content" | "fill";
  gapBeforeMode: ComponentStackGapMode;
  gapBeforeToken: string;
  gapBeforeWeight: number;
  alternatives: ComponentStackAlternativeContract[];
}

export interface ComponentStackDesignContract {
  id: "componentStack";
  sizingMode: ComponentStackSizingMode;
  startGapToken: string;
  endGapToken: string;
  slots: ComponentStackSlotContract[];
}

export type ComponentStackChildRenderer = ComponentCollectionChildRenderer;
