import type {
  ComponentCollectionAlignment,
  ComponentCollectionChildRenderer,
  ComponentCollectionGapMode,
  ComponentCollectionItemContract,
  ComponentCollectionSizingMode,
} from "./componentCollectionContract.js";
import type { ComponentMotionContract } from "./previewComponentContracts.js";

export type ComponentStackSizingMode = ComponentCollectionSizingMode;
export type ComponentStackAlignment = ComponentCollectionAlignment;
export type ComponentStackGapMode = ComponentCollectionGapMode;

export interface ComponentStackAlternativeContract {
  id: string;
  component?: ComponentCollectionItemContract;
  behavior: "replace" | "overlay";
  active: boolean;
  isDefault: boolean;
  enterMotion: ComponentMotionContract;
  exitMotion: ComponentMotionContract;
  activationFrame?: number;
  exitFrame?: number;
}

export interface ComponentStackSlotContract {
  id: string;
  alignment: ComponentStackAlignment;
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
