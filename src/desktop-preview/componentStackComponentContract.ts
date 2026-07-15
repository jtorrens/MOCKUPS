import type {
  ComponentCollectionAlignment,
  ComponentCollectionChildRenderer,
  ComponentCollectionGapMode,
  ComponentCollectionItemContract,
  ComponentCollectionSizingMode,
} from "./componentCollectionContract.js";

export type ComponentStackSizingMode = ComponentCollectionSizingMode;
export type ComponentStackAlignment = ComponentCollectionAlignment;
export type ComponentStackGapMode = ComponentCollectionGapMode;
export type ComponentStackItemContract = ComponentCollectionItemContract;

export interface ComponentStackDesignContract {
  id: "componentStack";
  sizingMode: ComponentStackSizingMode;
  startGapToken: string;
  endGapToken: string;
  items: ComponentStackItemContract[];
}

export type ComponentStackChildRenderer = ComponentCollectionChildRenderer;
