import type {
  ComponentCollectionChildRenderer,
  ComponentCollectionItemContract,
  ComponentCollectionSizingMode,
} from "./componentCollectionContract.js";

export type CollectionStackDistributionMode = "flow" | "stacked";
export type CollectionStackDirection = "down" | "up";
export type CollectionStackItemSizingMode = "intrinsic" | "largest";

export interface CollectionStackDesignContract {
  id: "collectionStack";
  distributionMode: CollectionStackDistributionMode;
  sizingMode: ComponentCollectionSizingMode;
  startGapToken: string;
  endGapToken: string;
  stackDirection: CollectionStackDirection;
  stackOffsetToken: string;
  itemSizingMode: CollectionStackItemSizingMode;
  scaleRatio: number;
  opacityRatio: number;
  items: ComponentCollectionItemContract[];
  reflow?: {
    progress: number;
    fromItems: ComponentCollectionItemContract[];
    fromDistributionMode?: CollectionStackDistributionMode;
  };
}

export type CollectionStackChildRenderer = ComponentCollectionChildRenderer;
