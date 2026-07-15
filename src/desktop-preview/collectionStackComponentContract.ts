import type {
  ComponentCollectionChildRenderer,
  ComponentCollectionItemContract,
  ComponentCollectionSizingMode,
} from "./componentCollectionContract.js";

export type CollectionStackDistributionMode = "flow" | "stacked";
export type CollectionStackDirection = "down" | "up";

export interface CollectionStackDesignContract {
  id: "collectionStack";
  distributionMode: CollectionStackDistributionMode;
  sizingMode: ComponentCollectionSizingMode;
  startGapToken: string;
  endGapToken: string;
  stackDirection: CollectionStackDirection;
  stackOffsetToken: string;
  items: ComponentCollectionItemContract[];
}

export type CollectionStackChildRenderer = ComponentCollectionChildRenderer;
